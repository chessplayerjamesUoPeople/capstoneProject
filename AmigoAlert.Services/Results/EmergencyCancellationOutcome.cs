using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of the user pressing "Cancel emergency, I am okay".
	/// </summary>
	public enum EmergencyCancellationOutcome : byte
	{
		/// <summary>The status was returned to okay and, when a consented contact exists, the provider accepted the stand-down message.</summary>
		Cancelled = 1,

		/// <summary>No user has the given identifier.</summary>
		UserNotFound = 2,

		/// <summary>The user is neither escalated nor in an emergency, so there is nothing to cancel. Nothing was written.</summary>
		NotInEmergency = 3,

		/// <summary>The provider rejected the stand-down message. Nothing was written, so it can be retried.</summary>
		NotificationFailed = 4
	}
}
