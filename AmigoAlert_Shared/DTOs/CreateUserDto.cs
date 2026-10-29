using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// A registration request. The identifier, initial status and terms acceptance time are assigned by the server; submitting the request
	/// constitutes acceptance of <paramref name="AcceptedTermsVersion"/>. <paramref name="VerificationCode"/> is the registration code
	/// emailed to <paramref name="Email"/>, proving the user owns the address.
	/// </summary>
	public sealed record CreateUserDto([Required, StringLength(50)] string Name, [Required, EmailAddress, StringLength(100)] string Email,
		[StringLength(20), RegularExpression(PhoneNumberFormat.E164)] string? Phone, [Required, StringLength(200)] string Address,
		[StringLength(150)] string? Landmarks, TimeOnly? Notification1Time, TimeOnly? Notification2Time,
		[Required, StringLength(10)] string AcceptedTermsVersion,
		[Required, RegularExpression(VerificationCodeFormat.Pattern)] string VerificationCode);
}
