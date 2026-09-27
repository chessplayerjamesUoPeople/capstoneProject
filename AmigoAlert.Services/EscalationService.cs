using AmigoAlert.Data.Repositories;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Represents a service responsible for handling escalation-related operations within the AmigoAlert application.
	/// </summary>
	/// <remarks>
	/// Beginning with GET operations only.
	/// *********************************************************************************
	/// TODO: Implement POST and PUT operations for escalation management in the future.
	/// *********************************************************************************
	/// </remarks>
	internal sealed class EscalationService(IUserRepository userRepository, ICheckInRepository checkInRepository)
	{
		/// <summary>
		/// Evaluates every eligible user against the escalation rules and returns
		/// the resulting decisions. Performs no writes and dispatches nothing.
		/// </summary>
		public async Task<IReadOnlyList<EscalationDecision>> EvaluateDueUsersAsync(
			DateTimeOffset now, CancellationToken ct = default)
		{
			var users = await userRepository.GetActiveUsersWithContactsAsync(ct);
			if (users.Count == 0)
			{
				return [];
			}

			var latestCheckIns = await checkInRepository.GetLatestForUsersAsync(
				users.Select(u => u.Id), ct);

			var byUser = latestCheckIns.ToDictionary(c => c.UserId);

			var decisions = new List<EscalationDecision>(users.Count);

			foreach (var user in users)
			{
				byUser.TryGetValue(user.Id, out var latest);

				var input = new EscalationRules.Input(
					FirstNotificationTime: user.Notification1Time,
					SecondNotificationTime: user.Notification2Time,
					Status: (StatusType)user.StatusId,
					LatestScheduledFor: latest?.ScheduledFor,
					LatestTimeReceived: latest?.TimeReceived);

				var result = EscalationRules.Evaluate(in input, now);

				if (result.Outcome != EscalationOutcome.None)
				{
					decisions.Add(new EscalationDecision(
						UserId: user.Id,
						Outcome: result.Outcome,
						ScheduledFor: result.ScheduledFor,
						EvaluatedAt: now,
						CheckInId: latest?.Id));
				}
			}

			return decisions;
		}
	}
}
