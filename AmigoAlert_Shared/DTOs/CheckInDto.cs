using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	public sealed record CheckInDto(long Id, DateTimeOffset ScheduledFor, DateTimeOffset TimeSent,
		DateTimeOffset? TimeReceived, CheckInResponseType Response, DateTimeOffset? TimeDelivered);
}
