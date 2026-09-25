using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.API.Mapping
{
	/// <summary>
	/// Provides extension methods for mapping UserDto objects.
	/// </summary>
	internal static class CheckInMappingExtensions
	{
		/// <summary>
		/// Maps a CheckIn object to a new CheckInDto instance.
		/// </summary>
		/// <param name="CheckIn">The CheckIn object to map.</param>
		/// <returns>A new CheckInDto object with the same values mapped, from the original object.</returns>
		public static CheckInDto ToDto(this CheckIn CheckIn) => new CheckInDto(CheckIn.Id, CheckIn.ScheduledFor,
			CheckIn.TimeSent, CheckIn.TimeReceived, (CheckInResponseType)CheckIn.ResponseId);
	}
}
