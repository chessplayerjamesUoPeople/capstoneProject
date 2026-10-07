using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The push channel of one device, registered so check-in notifications reach it. The user and the device are identified by the route,
	/// and the server decides which notifications the device receives; a client cannot choose its own tags.
	/// </summary>
	/// <param name="PushChannel">The FCM registration token. FCM documents no maximum length, so the limit is generous.</param>
	public sealed record RegisterDeviceDto([Required, StringLength(4096, MinimumLength = 1)] string PushChannel);
}
