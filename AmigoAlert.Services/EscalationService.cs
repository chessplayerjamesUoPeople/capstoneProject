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
		ITransactionProvider transactionProvider, TimeProvider timeProvider)
	{
		/// <summary>WGS 84, the coordinate system device GPS reports in.</summary>
		private const int Wgs84Srid = 4326;

		private static readonly StatusType[] AwaitingReminder = [StatusType.Pending1];

		private static readonly StatusType[] AwaitingResponse = [StatusType.Pending1, StatusType.Pending2];

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
					LatestScheduledFor: latest?.ScheduledFor, LatestTimeReceived: latest?.TimeReceived);

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
			await userRepository.AddStatusLogAsync(NewStatusLog(checkIn.UserId, StatusType.Okay, now, "Check-in answered."), ct);
			await transaction.CommitAsync(ct);

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

			string body = SmsMessageComposer.EmergencyActivation(user.Name, latitude, longitude, user.Address);
			SmsDispatchResult dispatch = await smsService.SendAsync(contact.Id, contact.Phone, body, SmsMessageTypeEnum.EmergencyActivation,
				log.Id, ct);

			// Committing Emergency without a delivered alert would be worse than nothing: the rules suppress escalation for that status.
			if (!dispatch.Succeeded)
			{
				return EmergencyActivationOutcome.AlertFailed;
			}

			await transaction.CommitAsync(ct);

			return EmergencyActivationOutcome.Alerted;
		}

		private async Task<bool> SendFirstNotificationAsync(EscalationDecision decision, CancellationToken ct)
		{
			DateTimeOffset now = timeProvider.GetUtcNow();

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			await checkInRepository.AddAsync(NewCheckIn(decision, now), ct);

			// Unconditional: a user left at Escalated or Emergency by an earlier occurrence must be moved off it, or the rules' guard would
			// suppress escalation for this occurrence too.
			await userRepository.UpdateStatusAsync(decision.UserId, StatusType.Pending1, ct);
			await userRepository.AddStatusLogAsync(NewStatusLog(decision.UserId, StatusType.Pending1, now, "Check-in notification sent."),
				ct);
			await transaction.CommitAsync(ct);

			// TODO: Dispatch the push notification through Azure Notification Hubs once it is wired.
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
			await checkInRepository.AddAsync(NewCheckIn(decision, now), ct);
			await userRepository.AddStatusLogAsync(NewStatusLog(decision.UserId, StatusType.Pending2, now, "Reminder notification sent."),
				ct);
			await transaction.CommitAsync(ct);

			// TODO: Dispatch the push notification through Azure Notification Hubs once it is wired.
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

			StatusLog log = NewStatusLog(user.Id, StatusType.Escalated, now, "Missed check-in escalated to the emergency contact.");
			await userRepository.AddStatusLogAsync(log, ct);

			string body = SmsMessageComposer.MissedCheckIn(user.Name, decision.ScheduledFor);
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
				TimeSent = sentAt,
				ResponseId = (byte)CheckInResponseType.NoResponse
			};
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
