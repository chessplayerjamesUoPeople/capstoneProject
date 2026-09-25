using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.API.Mapping
{
	/// <summary>
	/// Provides extension methods for mapping UserDto objects.
	/// </summary>
	internal static class UserMappingExtensions
	{
		/// <summary>
		/// Maps a UserDto object to a new UserDto instance.
		/// </summary>
		/// <param name="user">The UserDto object to map.</param>
		/// <returns>A new UserDto instance with the same values as the input.</returns>
		public static UserDto ToDto(this User user)
			=> new(user.Id, user.Name, user.Email, user.Phone, user.Address, user.Landmarks, user.Notification1Time,
				user.Notification2Time, (StatusType)user.StatusId, user.AcceptedTermsTime);
	}
}
