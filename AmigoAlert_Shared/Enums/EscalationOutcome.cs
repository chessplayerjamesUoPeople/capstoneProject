using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// The action the escalation rules determined is required for a user at a given point in time.
	/// </summary>
	public enum EscalationOutcome : byte
	{
		/// <summary>No action required.</summary>
		None = 0,

		/// <summary>The check-in window has opened; send the first notification.</summary>
		SendFirstNotification = 1,

		/// <summary>The first notification went unanswered; send the reminder.</summary>
		SendReminderNotification = 2,

		/// <summary>The grace period has expired without a response; alert the contact.</summary>
		EscalateToEmergencyContact = 3
	}
}
