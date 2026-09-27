using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The device location at the moment the emergency button was pressed, in WGS 84 decimal degrees. Both values are optional, but the
	/// location is only used when both are supplied.
	/// </summary>
	public sealed record ActivateEmergencyDto([Range(-90d, 90d)] double? Latitude, [Range(-180d, 180d)] double? Longitude);
}
