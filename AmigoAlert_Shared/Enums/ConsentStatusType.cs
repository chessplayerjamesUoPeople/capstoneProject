using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// Represents the possible consent statuses for emergency contacts in the application.
	/// </summary>
	public enum ConsentStatusType : byte
	{
		/// <summary>
		/// The consent request is pending and has not yet been responded to by the user.
		/// </summary>
		Pending = 1,

		/// <summary>
		/// The user has granted consent.
		/// </summary>
		Granted = 2,

		/// <summary>
		/// The user has rejected consent.
		/// </summary>
		Rejected = 3,

		/// <summary>
		/// The consent token has expired without a response.
		/// </summary>
		Expired = 4,

		/// <summary>
		/// The consent was withdrawn after previously being granted.
		/// </summary>
		Revoked = 5
	}
}
