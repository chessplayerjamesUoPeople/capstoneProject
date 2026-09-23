using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// Represents the possible responses to a check-in event/notification sent to the user.
	/// </summary>
	public enum CheckInResponseType : byte
	{
		/// <summary>
		/// The user explicitly indicated that they are okay.
		/// </summary>
		Okay = 1,

		/// <summary>
		/// The user indicated that they are in an emergency situation and need immediate assistance.
		/// </summary>
		Emergency = 2,

		/// <summary>
		/// The user did not provide any response within the allotted time.
		/// </summary>
		NoResponse = 3
	}
}
