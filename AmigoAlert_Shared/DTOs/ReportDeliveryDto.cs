using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// When a check-in notification arrived on the device. The check-in is identified by the route.
	/// </summary>
	/// <param name="DeliveredAt">The instant the device received the notification, by the device's own clock.</param>
	public sealed record ReportDeliveryDto(DateTimeOffset DeliveredAt);
}
