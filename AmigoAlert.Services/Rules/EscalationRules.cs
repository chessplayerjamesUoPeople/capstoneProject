using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Rules
{
	/// <summary>
	/// Pure escalation decision logic. No dependencies, no I/O, fully deterministic.
	/// This is the unit under test for escalation decision correctness.
	/// </summary>
	public static class EscalationRules
	{
		/// <summary>Ecuador (mainland) observes UTC-5 year-round with no daylight saving.</summary>
		public static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-5);

		/// <summary>Minutes after the first notification was sent before the reminder is sent.</summary>
		public const int ReminderAfterMinutes = 10;

		/// <summary>Minutes after the reminder was sent before the emergency contact is alerted.</summary>
		public const int GraceMinutes = 10;

		/// <summary>The state required to evaluate one user at one point in time.</summary>
		public readonly record struct Input(TimeOnly? FirstNotificationTime, TimeOnly? SecondNotificationTime,
			StatusType Status, DateTimeOffset? LatestScheduledFor, DateTimeOffset? LatestTimeSent, DateTimeOffset? LatestTimeReceived);

		/// <summary>The outcome plus the occurrence it relates to.</summary>
		public readonly record struct Result(EscalationOutcome Outcome, DateTimeOffset ScheduledFor);

		/// <summary>
		/// Determines what action, if any, is required for a user at the given instant.
		/// </summary>
		public static Result Evaluate(in Input input, DateTimeOffset now)
		{
			DateTimeOffset? occurrence = MostRecentOccurrence(input.FirstNotificationTime, input.SecondNotificationTime, now);

			// No configured schedule, or the first occurrence has not arrived yet.
			if (occurrence is null)
			{
				return new Result(EscalationOutcome.None, default);
			}

			DateTimeOffset due = occurrence.Value;

			// The newest check-in belongs to an earlier occurrence, so this one has not been notified yet.
			if (input.LatestScheduledFor != due)
			{
				return new Result(EscalationOutcome.SendFirstNotification, due);
			}

			// The user answered this occurrence.
			if (input.LatestTimeReceived is not null)
			{
				return new Result(EscalationOutcome.None, due);
			}

			// The contact has already been alerted, either by escalation or by the
			// user pressing the emergency button. Do not alert a second time.
			if (input.Status is StatusType.Escalated or StatusType.Emergency)
			{
				return new Result(EscalationOutcome.None, due);
			}

			// Waits run from when the latest notification was actually sent, not from the scheduled time, so a late send still gives
			// the user the full window. The latest check-in is the first notification while Pending1 and the reminder once Pending2.
			if (input.LatestTimeSent is not DateTimeOffset sent)
			{
				return new Result(EscalationOutcome.None, due);
			}

			if (input.Status == StatusType.Pending2 && now >= sent.AddMinutes(GraceMinutes))
			{
				return new Result(EscalationOutcome.EscalateToEmergencyContact, due);
			}

			// A reminder that was missed (for example the worker was down) is still sent before escalating.
			if (input.Status == StatusType.Pending1 && now >= sent.AddMinutes(ReminderAfterMinutes))
			{
				return new Result(EscalationOutcome.SendReminderNotification, due);
			}

			return new Result(EscalationOutcome.None, due);
		}

		/// <summary>
		/// The latest configured check-in instant at or before <paramref name="now"/>,
		/// looking back across the previous local day to cover occurrences that straddle midnight UTC.
		/// </summary>
		public static DateTimeOffset? MostRecentOccurrence(TimeOnly? first, TimeOnly? second, DateTimeOffset now)
		{
			DateTimeOffset localNow = now.ToOffset(LocalOffset);
			DateTimeOffset? best = null;

			for (int dayShift = 0; dayShift >= -1; dayShift--)
			{
				DateOnly date = DateOnly.FromDateTime(localNow.Date).AddDays(dayShift);

				foreach (TimeOnly? time in new[] { first, second })
				{
					if (time is null)
					{
						continue;
					}

					DateTimeOffset candidate = new DateTimeOffset(date.ToDateTime(time.Value), LocalOffset);

					if (candidate <= now && (best is null || candidate > best))
					{
						best = candidate;
					}
				}
			}

			return best;
		}
	}
}
