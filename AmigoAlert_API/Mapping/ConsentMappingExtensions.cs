using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.API.Mapping
{
	internal static class ConsentMappingExtensions
	{
		public static ConsentRequestDto ToConsentRequestDto(this EmergencyContact contact, DateTimeOffset now)
		{
			return new ConsentRequestDto(contact.Name, contact.User.Name, (ConsentStatusType)contact.ConsentStatusId,
				ConsentService.CanRespond(contact, now));
		}
	}
}
