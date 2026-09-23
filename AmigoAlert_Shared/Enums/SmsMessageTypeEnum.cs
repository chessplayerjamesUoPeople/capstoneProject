using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// Represents the different types of SMS messages that can be sent to an emergency contact.
	/// </summary>
	public enum SmsMessageTypeEnum : byte
	{
		/// <summary>
		/// An SMS message sent requesting consent from the emergency contact to receive future messages.
		/// </summary>
		ConsentRequest = 1,

		/// <summary>
		/// An SMS message sent to the emergency contact when a user has missed a check-in, prompting them to take action.
		/// </summary>
		MissedCheckInEscalation = 2,

		/// <summary>
		/// An SMS message sent to the emergency contact when a user has activated an emergency alert,
		/// notifying them of the situation.
		/// </summary>
		EmergencyActivation = 3,

		/// <summary>
		/// An SMS message sent to the emergency contact when a user has cancelled an emergency alert.
		/// </summary>
		EmergencyCancelled = 4
	}
}
