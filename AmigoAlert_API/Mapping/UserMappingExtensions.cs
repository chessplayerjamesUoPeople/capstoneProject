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

		/// <summary>
		/// Maps a registration request to a new User entity. The identifier, status and terms acceptance time are left for
		/// <c>RegistrationService</c> to assign.
		/// </summary>
		/// <param name="request">The registration request to map.</param>
		/// <returns>A new, unsaved User entity.</returns>
		public static User ToEntity(this CreateUserDto request)
			=> new() { Name = request.Name, Email = request.Email, Phone = request.Phone, Address = request.Address,
				Landmarks = request.Landmarks, Notification1Time = request.Notification1Time, Notification2Time = request.Notification2Time,
				AcceptedTermsVersion = request.AcceptedTermsVersion };
	}
}
