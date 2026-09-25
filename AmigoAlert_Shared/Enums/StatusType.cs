using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// Represents the possible statuses in the application.
	/// </summary>
	public enum StatusType : byte
	{
		/// <summary>
		/// The user is in a normal state, no issues reported.
		/// They have responded to the last check-in and are considered to be okay.
		/// </summary>
		Okay = 1,

		/// <summary>
		/// A user response is pending the first check-in.
		/// </summary>
		Pending1 = 2,

		/// <summary>
		/// A user response is pending the second check-in.
		/// This could indicate a potential issue, as the user has not responded to two consecutive check-ins.
		/// </summary>
		Pending2 = 3,

		/// <summary>
		/// The user has not responded to the check-ins and the situation has been escalated to the emergency contact.
		/// </summary>
		Escalated = 4,

		/// <summary>
		/// The user has pressed the emergency button, indicating an immediate need for assistance.
		/// </summary>
		Emergency = 5
	}
}
