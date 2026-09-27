using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of adding or replacing a user's emergency contact.
	/// </summary>
	public enum EmergencyContactChangeOutcome : byte
	{
		/// <summary>A new contact was created at pending consent and the consent request SMS was accepted by the provider.</summary>
		ConsentRequested = 1,

		/// <summary>Only the name changed; the existing contact and its consent were kept.</summary>
		NameUpdated = 2,

		/// <summary>No user has the given identifier.</summary>
		UserNotFound = 3,

		/// <summary>A replacement was requested but the user has no active contact.</summary>
		ContactNotFound = 4,

		/// <summary>A first contact was requested but the user already has an active contact.</summary>
		ContactAlreadyExists = 5,

		/// <summary>The provider rejected the consent request SMS. Nothing was written and any previous contact is unchanged.</summary>
		SmsFailed = 6
	}
}
