using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The emergency contact to add or replace. Changing <paramref name="Phone"/> starts a new consent request; changing only
	/// <paramref name="Name"/> keeps the existing consent.
	/// </summary>
	public sealed record SetEmergencyContactDto([Required, StringLength(50)] string Name,
		[Required, StringLength(20), RegularExpression(PhoneNumberFormat.E164)] string Phone);
}
