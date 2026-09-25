using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	public sealed record UserDto(Guid Id, string Name, string Email, string? Phone, string Address, string? Landmarks,
		TimeOnly? Notification1Time, TimeOnly? Notification2Time, StatusType Status, DateTimeOffset AcceptedTerms);
}
