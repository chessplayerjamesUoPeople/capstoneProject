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
		/// <param name="User">The UserDto object to map.</param>
		/// <returns>A new UserDto instance with the same values as the input.</returns>
		public static UserDto ToDto(this User User)
			=> new(User.Id, User.Name, User.Email, User.Phone, User.Address, User.Landmarks, User.Notification1Time,
				User.Notification2Time, (StatusType)User.StatusId, User.AcceptedTermsTime);
	}
}
