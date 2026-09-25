using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The active emergency contact for a user, as exposed to the mobile client.
	/// Consent token fields are deliberately excluded.
	/// </summary>
	public sealed record EmergencyContactDto(Guid Id, string Name, string Phone, ConsentStatusType ConsentStatus,
		DateTimeOffset ConsentRequestedAt, DateTimeOffset? ConsentRespondedAt);
}
