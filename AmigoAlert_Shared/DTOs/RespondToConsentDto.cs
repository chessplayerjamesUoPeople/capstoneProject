using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The emergency contact's answer to a consent request: true grants consent to be messaged, false rejects it.
	/// </summary>
	public sealed record RespondToConsentDto(bool Granted);
}
