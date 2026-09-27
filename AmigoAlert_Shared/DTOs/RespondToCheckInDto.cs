using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The user's answer to a check-in notification. Only <see cref="CheckInResponseType.Okay"/> is accepted: an emergency is raised
	/// through the emergency endpoint, which also captures location and alerts the contact.
	/// </summary>
	public sealed record RespondToCheckInDto([EnumDataType(typeof(CheckInResponseType))] CheckInResponseType Response);
}
