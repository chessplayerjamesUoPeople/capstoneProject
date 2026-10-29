using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// A log in request: the address the user registered with and the log in code emailed to it.
	/// </summary>
	/// <param name="Email">The address the user registered with.</param>
	/// <param name="VerificationCode">The six-digit log in code.</param>
	public sealed record LogInDto([Required, EmailAddress, StringLength(100)] string Email,
		[Required, RegularExpression(VerificationCodeFormat.Pattern)] string VerificationCode);
}
