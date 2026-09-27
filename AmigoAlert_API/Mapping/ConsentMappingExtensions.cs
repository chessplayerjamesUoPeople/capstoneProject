using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.API.Mapping
{
	internal static class ConsentMappingExtensions
	{
		public static ConsentRequestDto ToConsentRequestDto(this EmergencyContact contact, DateTimeOffset now)
		{
			ConsentStatusType status = (ConsentStatusType)contact.ConsentStatusId;

			bool canRespond = status == ConsentStatusType.Pending && contact.ConsentTokenExpires is not null &&
				contact.ConsentTokenExpires > now;

			return new ConsentRequestDto(contact.Name, contact.User.Name, status, canRespond);
		}
	}
}
