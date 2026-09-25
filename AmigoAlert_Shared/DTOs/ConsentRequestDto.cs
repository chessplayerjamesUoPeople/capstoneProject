using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The minimum information the consent page needs to render: who the request
	/// is on behalf of, and whether it can still be acted on.
	/// </summary>
	public sealed record ConsentRequestDto(string ContactName, string UserName, ConsentStatusType ConsentStatus, bool CanRespond);
}
