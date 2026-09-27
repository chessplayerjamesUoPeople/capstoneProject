using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of the user pressing the emergency button.
	/// </summary>
	public enum EmergencyActivationOutcome : byte
	{
		/// <summary>The status was set to emergency and the provider accepted the alert to the contact.</summary>
		Alerted = 1,

		/// <summary>No user has the given identifier.</summary>
		UserNotFound = 2,

		/// <summary>The user has no active contact who has granted consent, so no one may be messaged. Nothing was written.</summary>
		NoConsentedContact = 3,

		/// <summary>The provider rejected the alert. Nothing was written, so it can be retried and escalation still applies.</summary>
		AlertFailed = 4
	}
}
