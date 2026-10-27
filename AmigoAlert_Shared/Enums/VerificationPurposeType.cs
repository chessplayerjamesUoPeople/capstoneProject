using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.Enums
{
	/// <summary>
	/// What an email verification code was issued for. A code issued for one purpose cannot be used for the other.
	/// </summary>
	public enum VerificationPurposeType : byte
	{
		/// <summary>
		/// Proves the user owns the email address they are registering with.
		/// </summary>
		Registration = 1,

		/// <summary>
		/// Logs an existing user in, for example on a new device or after the app's data was cleared.
		/// </summary>
		Login = 2
	}
}
