using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// A request to email a one-time verification code. A registration code is sent only to an unregistered address and a log in code only
	/// to a registered one.
	/// </summary>
	/// <param name="Email">The address to send the code to.</param>
	/// <param name="Purpose">What the code will be used for.</param>
	public sealed record RequestVerificationCodeDto([Required, EmailAddress, StringLength(100)] string Email,
		[EnumDataType(typeof(VerificationPurposeType))] VerificationPurposeType Purpose);
}
