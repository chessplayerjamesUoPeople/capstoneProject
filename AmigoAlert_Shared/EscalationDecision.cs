using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared
{
	/// <summary>
	/// The outcome of evaluating a single user against the escalation rules
	/// at a point in time. Immutable and free of persistence concerns.
	/// </summary>
	public sealed record EscalationDecision(Guid UserId, EscalationOutcome Outcome, DateTimeOffset ScheduledFor,
		DateTimeOffset EvaluatedAt, long? CheckInId)
	{
		/// <summary>True when the decision requires the caller to act.</summary>
		public bool IsActionable => Outcome != EscalationOutcome.None;

		/// <summary>
		/// How late the evaluation is relative to the scheduled check-in.
		/// Supports the alert-latency metric.
		/// </summary>
		public TimeSpan Latency => EvaluatedAt - ScheduledFor;
	}
}
