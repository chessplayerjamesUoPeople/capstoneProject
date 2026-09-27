using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The user-editable profile. The identifier comes from the route; email, status, terms acceptance and the notification schedule are
	/// not editable through this request.
	/// </summary>
	public sealed record UpdateUserDto([Required, StringLength(50)] string Name,
		[StringLength(20), RegularExpression(PhoneNumberFormat.E164)] string? Phone, [Required, StringLength(200)] string Address,
		[StringLength(150)] string? Landmarks);
}
