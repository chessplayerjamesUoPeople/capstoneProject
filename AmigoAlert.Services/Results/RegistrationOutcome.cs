using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of registering a new user.
	/// </summary>
	public enum RegistrationOutcome : byte
	{
		/// <summary>The user and their initial status log row were written, and the verification code was used up.</summary>
		Registered = 1,

		/// <summary>The email address is already registered. Nothing was written and the code was not used.</summary>
		EmailAlreadyRegistered = 2,

		/// <summary>
		/// The verification code is wrong, expired, already used or superseded, or too many wrong codes were entered. Nothing was written.
		/// </summary>
		InvalidVerificationCode = 3
	}
}
