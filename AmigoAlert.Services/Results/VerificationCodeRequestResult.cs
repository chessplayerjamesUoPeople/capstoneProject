using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The outcome of asking for a verification code to be emailed, with how long to wait when the request was refused as too frequent.
	/// </summary>
	/// <param name="Outcome">What happened.</param>
	/// <param name="RetryAfter">With <see cref="VerificationCodeRequestOutcome.TooManyRequests"/>, how long until a request can succeed;
	/// otherwise null.</param>
	public sealed record VerificationCodeRequestResult(VerificationCodeRequestOutcome Outcome, TimeSpan? RetryAfter = null);
}
