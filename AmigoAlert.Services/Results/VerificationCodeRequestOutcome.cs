using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of asking for a verification code to be emailed.
	/// </summary>
	public enum VerificationCodeRequestOutcome : byte
	{
		/// <summary>A new code was recorded and the provider accepted the email. Any earlier code for the same purpose no longer works.</summary>
		Sent = 1,

		/// <summary>A registration code was requested for an address that is already registered. Nothing was sent.</summary>
		EmailAlreadyRegistered = 2,

		/// <summary>A log in code was requested for an address no user is registered with. Nothing was sent.</summary>
		EmailNotRegistered = 3,

		/// <summary>A code was sent too recently, or too many were sent in the last hour. Nothing was sent.</summary>
		TooManyRequests = 4,

		/// <summary>The provider rejected the email. Nothing was recorded, so the request can be retried at once.</summary>
		SendFailed = 5
	}
}
