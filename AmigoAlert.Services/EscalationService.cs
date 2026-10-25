using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services.Results;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Evaluates users against <see cref="EscalationRules"/> and carries out the resulting state changes: recording notifications and
	/// responses, escalating missed check-ins to the emergency contact, and the manual emergency button.
	/// </summary>
	/// <remarks>
	/// <see cref="StatusType"/> is held per user, not per occurrence, so every write here must leave the status where the rules expect it
	/// on the next evaluation. A new occurrence moves the user off <see cref="StatusType.Escalated"/>, or the rules' guard would suppress
	/// its escalation; a reminder moves the user to <see cref="StatusType.Pending2"/>, or the reminder branch would fire on every run.
	/// <para>
	/// SMS is dispatched inside the transaction and the transaction commits only if the provider accepted the message. A rejected alert
	/// therefore leaves no state behind claiming the contact was told, and the operation can be retried.
	/// </para>
	/// </remarks>
	public sealed class EscalationService(IUserRepository userRepository, ICheckInRepository checkInRepository, ISmsService smsService,
		IPushNotificationService pushService, ITransactionProvider transactionProvider, TimeProvider timeProvider)
	{
		/// <summary>WGS 84, the coordinate system device GPS reports in.</summary>
		private const int Wgs84Srid = 4326;

		private static readonly StatusType[] AwaitingReminder = [StatusType.Pending1];

		private static readonly StatusType[] AwaitingResponse = [StatusType.Pending1, StatusType.Pending2];

		private static readonly StatusType[] AlertRaised = [StatusType.Escalated, StatusType.Emergency];

		/// <summary>
		/// Evaluates every eligible user against the escalation rules and returns the resulting decisions. Performs no writes and
		/// dispatches nothing; pass each decision to <see cref="ActOnDecisionAsync"/> to carry it out.
		/// </summary>
		/// <param name="now">The instant to evaluate at.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The actionable decisions, one per user at most.</returns>
		public async Task<IReadOnlyList<EscalationDecision>> EvaluateDueUsersAsync(DateTimeOffset now, CancellationToken ct = default)
		{
			IReadOnlyList<User> users = await userRepository.GetActiveUsersWithContactsAsync(ct);
			if (users.Count == 0)
			{
				return [];
			}

			IReadOnlyList<CheckIn> latestCheckIns = await checkInRepository.GetLatestForUsersAsync(users.Select(u => u.Id), ct);

			Dictionary<Guid, CheckIn> byUser = latestCheckIns.ToDictionary(c => c.UserId);

			List<EscalationDecision> decisions = new List<EscalationDecision>(users.Count);

			foreach (User user in users)
			{
				byUser.TryGetValue(user.Id, out CheckIn? latest);

				EscalationRules.Input input = new EscalationRules.Input(FirstNotificationTime: user.Notification1Time,
					SecondNotificationTime: user.Notification2Time, Status: (StatusType)user.StatusId,
					LatestScheduledFor: latest?.ScheduledFor, LatestTimeSent: latest?.TimeSent, LatestTimeReceived: latest?.TimeReceived);

				EscalationRules.Result result = EscalationRules.Evaluate(in input, now);

				if (result.Outcome != EscalationOutcome.None)
				{
					decisions.Add(new EscalationDecision(UserId: user.Id, Outcome: result.Outcome, ScheduledFor: result.ScheduledFor,
						EvaluatedAt: now, CheckInId: latest?.Id));
				}
			}

			return decisions;
		}

		/// <summary>
		/// Carries out one decision from <see cref="EvaluateDueUsersAsync"/>. Reminders and escalations are guarded on the user's current
		/// status, so a decision that went stale because the user answered in the meantime writes nothing.
		/// </summary>
		/// <param name="decision">The decision to act on.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the decision was carried out; false when it was stale, not actionable, or its SMS was rejected.</returns>
		public Task<bool> ActOnDecisionAsync(EscalationDecision decision, CancellationToken ct = default)
		{
			return decision.Outcome switch
			{
				EscalationOutcome.SendFirstNotification => SendFirstNotificationAsync(decision, ct),
				EscalationOutcome.SendReminderNotification => SendReminderNotificationAsync(decision, ct),
				EscalationOutcome.EscalateToEmergencyContact => EscalateAsync(decision, ct),
				_ => Task.FromResult(false)
			};
		}

		/// <summary>
		/// Records the user's answer to a check-in and returns their status to <see cref="StatusType.Okay"/>, which moves the rules'
		/// outcome for the occurrence to <see cref="EscalationOutcome.None"/>.
		/// </summary>
		/// <param name="checkInId">The check-in the user answered: either the first notification or the reminder of the occurrence.</param>
		/// <param name="response">The user's response. Only <see cref="CheckInResponseType.Okay"/> is accepted.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the answer was recorded, or why not.</returns>
		public async Task<CheckInResponseOutcome> RecordCheckInResponseAsync(long checkInId, CheckInResponseType response,
			CancellationToken ct = default)
		{
			// An emergency must go through ActivateEmergencyAsync, which checks consent, captures location and alerts the contact;
			// NoResponse is what the system records, never what a user answers.
			if (response != CheckInResponseType.Okay)
			{
				return CheckInResponseOutcome.UnsupportedResponse;
			}

			CheckIn? checkIn = await checkInRepository.GetByIdAsync(checkInId, ct);
			if (checkIn is null)
			{
				return CheckInResponseOutcome.NotFound;
			}

			if (checkIn.TimeReceived is not null)
			{
				return CheckInResponseOutcome.AlreadyAnswered;
			}

			// Answering an older occurrence must not reset the status the current occurrence depends on.
			CheckIn? latest = await checkInRepository.GetLatestForUserAsync(checkIn.UserId, ct);
			if (latest is not null && latest.ScheduledFor != checkIn.ScheduledFor)
			{
				return CheckInResponseOutcome.Superseded;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			// Conditional on TimeReceived being null, so a duplicate answer, or one racing the WebJob, records nothing.
			if (await checkInRepository.RecordResponseAsync(checkInId, response, now, ct) == 0)
			{
				return CheckInResponseOutcome.AlreadyAnswered;
			}

			await userRepository.UpdateStatusAsync(checkIn.UserId, StatusType.Okay, ct);

			// The user is fine, so the locations the device reported for this occurrence have no use and are not kept.
			await userRepository.ClearCheckInLocationsAsync(checkIn.UserId, checkIn.ScheduledFor, ct);
			await userRepository.AddStatusLogAsync(NewStatusLog(checkIn.UserId, StatusType.Okay, now, "Check-in answered."), ct);
			await transaction.CommitAsync(ct);

			return CheckInResponseOutcome.Recorded;
		}

		/// <summary>
		/// Records a device location reported for a pending check-in, so that if the occurrence is escalated the alert can say where the
		/// user last was. Stored as a status log row in the user's current pending status: that is how
		/// <see cref="EscalateAsync"/> finds the newest one, and how <see cref="RecordCheckInResponseAsync"/> discards them.
		/// </summary>
		/// <param name="checkInId">The check-in the device was notified of: either the first notification or the reminder.</param>
		/// <param name="latitude">The device latitude, in WGS 84 decimal degrees.</param>
		/// <param name="longitude">The device longitude, in WGS 84 decimal degrees.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the location was recorded, or why not. <see cref="CheckInResponseOutcome.AlreadyAnswered"/> also covers an
		/// occurrence that has already been escalated, where a location would no longer reach the contact.</returns>
		public async Task<CheckInResponseOutcome> RecordCheckInLocationAsync(long checkInId, double latitude, double longitude,
			CancellationToken ct = default)
		{
			CheckIn? checkIn = await checkInRepository.GetByIdAsync(checkInId, ct);
			if (checkIn is null)
			{
				return CheckInResponseOutcome.NotFound;
			}

			if (checkIn.TimeReceived is not null)
			{
				return CheckInResponseOutcome.AlreadyAnswered;
			}

			CheckIn? latest = await checkInRepository.GetLatestForUserAsync(checkIn.UserId, ct);
			if (latest is not null && latest.ScheduledFor != checkIn.ScheduledFor)
			{
				return CheckInResponseOutcome.Superseded;
			}

			User? user = await userRepository.GetByIdAsync(checkIn.UserId, ct);
			if (user is null)
			{
				return CheckInResponseOutcome.NotFound;
			}

			// Written under the pending status the user holds now, which is what keeps these rows apart from emergency and escalation rows.
			StatusType status = (StatusType)user.StatusId;
			if (!AwaitingResponse.Contains(status))
			{
				return CheckInResponseOutcome.AlreadyAnswered;
			}

			Point location = new Point(longitude, latitude) { SRID = Wgs84Srid };
			await userRepository.AddStatusLogAsync(NewStatusLog(user.Id, status, timeProvider.GetUtcNow(), "Device location captured.", location),
				ct);

			return CheckInResponseOutcome.Recorded;
		}

		/// <summary>
		/// Handles the manual emergency button: sets the status to <see cref="StatusType.Emergency"/>, logs it with the location at the
		/// time of the press, and alerts the emergency contact. Requires an active contact who has granted consent; without one nothing is
		/// sent.
		/// </summary>
		/// <param name="userId">The user who pressed the button.</param>
		/// <param name="latitude">The device latitude at the time of the press, or null.</param>
		/// <param name="longitude">The device longitude at the time of the press, or null. Location is only recorded when both are
		/// supplied.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the contact was alerted, or why not.</returns>
		public async Task<EmergencyActivationOutcome> ActivateEmergencyAsync(Guid userId, double? latitude, double? longitude,
			CancellationToken ct = default)
		{
			User? user = await userRepository.GetWithEmergencyContactAsync(userId, ct);
			if (user is null)
			{
				return EmergencyActivationOutcome.UserNotFound;
			}

			// LOPDP Article 7: a contact who has not agreed to be messaged must not be.
			EmergencyContact? contact = GrantedContact(user);
			if (contact is null)
			{
				return EmergencyActivationOutcome.NoConsentedContact;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();
			Point? location = latitude is double lat && longitude is double lon ? new Point(lon, lat) { SRID = Wgs84Srid } : null;

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			await userRepository.UpdateStatusAsync(userId, StatusType.Emergency, ct);

			StatusLog log = NewStatusLog(userId, StatusType.Emergency, now, "Emergency button pressed.", location);
			await userRepository.AddStatusLogAsync(log, ct);

			foreach (string body in SmsMessageComposer.EmergencyActivationMessages(user.Name, latitude, longitude, user.Address, user.Landmarks))
			{
				SmsDispatchResult dispatch = await smsService.SendAsync(contact.Id, contact.Phone, body, SmsMessageTypeEnum.EmergencyActivation,
					log.Id, ct);

				// Committing Emergency without a delivered alert would be worse than nothing: the rules suppress escalation for that status.
				if (!dispatch.Succeeded)
				{
					return EmergencyActivationOutcome.AlertFailed;
				}
			}

			await transaction.CommitAsync(ct);

			return EmergencyActivationOutcome.Alerted;
		}

		/// <summary>
		/// Handles "Cancel emergency, I am okay": returns the user from <see cref="StatusType.Escalated"/> or
		/// <see cref="StatusType.Emergency"/> to <see cref="StatusType.Okay"/>, answers any unanswered check-in of the current occurrence,
		/// and tells the emergency contact to stand down.
		/// </summary>
		/// <param name="userId">The user who cancelled.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the emergency was cancelled, or why not.</returns>
		public async Task<EmergencyCancellationOutcome> CancelEmergencyAsync(Guid userId, CancellationToken ct = default)
		{
			User? user = await userRepository.GetWithEmergencyContactAsync(userId, ct);
			if (user is null)
			{
				return EmergencyCancellationOutcome.UserNotFound;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			if (await userRepository.TransitionStatusAsync(userId, AlertRaised, StatusType.Okay, ct) == 0)
			{
				return EmergencyCancellationOutcome.NotInEmergency;
			}

			// An escalated occurrence is still unanswered; left that way, the rules would keep treating it as missed.
			CheckIn? latest = await checkInRepository.GetLatestForUserAsync(userId, ct);
			if (latest is not null && latest.TimeReceived is null)
			{
				await checkInRepository.RecordResponseAsync(latest.Id, CheckInResponseType.Okay, now, ct);
			}

			StatusLog log = NewStatusLog(userId, StatusType.Okay, now, "Emergency cancelled by the user.");
			await userRepository.AddStatusLogAsync(log, ct);

			// LOPDP Article 7: only a contact who agreed to be messaged is told to stand down.
			EmergencyContact? contact = GrantedContact(user);
			if (contact is not null)
			{
				string body = SmsMessageComposer.EmergencyCancelled(user.Name);
				SmsDispatchResult dispatch = await smsService.SendAsync(contact.Id, contact.Phone, body,
					SmsMessageTypeEnum.EmergencyCancelled, log.Id, ct);

				// Returning to okay while the contact still believes there is an emergency would leave them acting on stale news.
				if (!dispatch.Succeeded)
				{
					return EmergencyCancellationOutcome.NotificationFailed;
				}
			}

			await transaction.CommitAsync(ct);

			return EmergencyCancellationOutcome.Cancelled;
		}

		private async Task<bool> SendFirstNotificationAsync(EscalationDecision decision, CancellationToken ct)
		{
			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			CheckIn checkIn = NewCheckIn(decision, now);
			await checkInRepository.AddAsync(checkIn, ct);

			// Unconditional: a user left at Escalated or Emergency by an earlier occurrence must be moved off it, or the rules' guard would
			// suppress escalation for this occurrence too.
			await userRepository.UpdateStatusAsync(decision.UserId, StatusType.Pending1, ct);
			await userRepository.AddStatusLogAsync(NewStatusLog(decision.UserId, StatusType.Pending1, now, "Check-in notification sent."),
				ct);
			await transaction.CommitAsync(ct);

			// After the commit, so the check-in exists before the notification can answer it. A failed push is logged by the service and
			// changes nothing: the reminder and escalation still follow for a user whose phone never received it.
			await pushService.SendCheckInAsync(decision.UserId, checkIn.Id, isReminder: false, now, ct);
			return true;
		}

		private async Task<bool> SendReminderNotificationAsync(EscalationDecision decision, CancellationToken ct)
		{
			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			// Advancing to Pending2 is what stops the reminder branch firing again on every run. Zero rows means the user answered after
			// the evaluation, so there is nothing to remind them of.
			if (await userRepository.TransitionStatusAsync(decision.UserId, AwaitingReminder, StatusType.Pending2, ct) == 0)
			{
				return false;
			}

			// A second row for the same occurrence, sharing ScheduledFor with the first notification.
			CheckIn checkIn = NewCheckIn(decision, now);
			await checkInRepository.AddAsync(checkIn, ct);
			await userRepository.AddStatusLogAsync(NewStatusLog(decision.UserId, StatusType.Pending2, now, "Reminder notification sent."),
				ct);
			await transaction.CommitAsync(ct);

			// As for the first notification. On the device it replaces the first notification.
			await pushService.SendCheckInAsync(decision.UserId, checkIn.Id, isReminder: true, now, ct);
			return true;
		}

		private async Task<bool> EscalateAsync(EscalationDecision decision, CancellationToken ct)
		{
			User? user = await userRepository.GetWithEmergencyContactAsync(decision.UserId, ct);

			// Consent may have been withdrawn, or the contact replaced, since the evaluation ran.
			EmergencyContact? contact = user is null ? null : GrantedContact(user);
			if (user is null || contact is null)
			{
				return false;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			if (await userRepository.TransitionStatusAsync(user.Id, AwaitingResponse, StatusType.Escalated, ct) == 0)
			{
				return false;
			}

			// The newest location the device reported for this occurrence, if it ever did. Without one the alert is sent as before.
			StatusLog? fix = await userRepository.GetLatestCheckInLocationAsync(user.Id, decision.ScheduledFor, ct);

			StatusLog log = NewStatusLog(user.Id, StatusType.Escalated, now, "Missed check-in escalated to the emergency contact.",
				fix?.Location);
			await userRepository.AddStatusLogAsync(log, ct);

			string body = SmsMessageComposer.MissedCheckIn(user.Name, decision.ScheduledFor, fix?.Location?.Y, fix?.Location?.X,
				fix?.TimeOccurrence);
			SmsDispatchResult dispatch = await smsService.SendAsync(contact.Id, contact.Phone, body,
				SmsMessageTypeEnum.MissedCheckInEscalation, log.Id, ct);

			// Rolling back leaves the user at Pending, so the next evaluation escalates again instead of treating an alert that never left
			// as sent.
			if (!dispatch.Succeeded)
			{
				return false;
			}

			await transaction.CommitAsync(ct);

			return true;
		}

		private static EmergencyContact? GrantedContact(User user)
		{
			return user.EmergencyContacts.FirstOrDefault(c => c.IsActive && c.ConsentStatusId == (byte)ConsentStatusType.Granted);
		}

		private static CheckIn NewCheckIn(EscalationDecision decision, DateTimeOffset sentAt)
		{
			return new CheckIn
			{
				UserId = decision.UserId,
				ScheduledFor = decision.ScheduledFor,
				TimeSent = BeforeRunMinute(sentAt),
				ResponseId = (byte)CheckInResponseType.NoResponse
			};
		}

		/// <summary>
		/// The second before the minute <paramref name="instant"/> falls in. The WebJob runs on the minute but takes a varying few seconds
		/// to start, so a wait measured from the exact send time can fall short by a second on the run that is due and slip to the next
		/// one. Measured from just before the sending run's minute, every wait is complete when the run it ends on begins.
		/// </summary>
		private static DateTimeOffset BeforeRunMinute(DateTimeOffset instant)
		{
			return new DateTimeOffset(instant.Ticks - instant.Ticks % TimeSpan.TicksPerMinute, instant.Offset).AddSeconds(-1);
		}

		private static StatusLog NewStatusLog(Guid userId, StatusType status, DateTimeOffset occurredAt, string notes,
			Point? location = null)
		{
			return new StatusLog
			{
				UserId = userId,
				TimeOccurrence = occurredAt,
				StatusId = (byte)status,
				Location = location,
				Notes = notes
			};
		}
	}
}
