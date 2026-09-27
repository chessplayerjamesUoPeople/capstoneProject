using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.API.Mapping
{
	internal static class EmergencyContactMappingExtensions
	{
		public static EmergencyContactDto ToDto(this EmergencyContact contact)
			=> new(contact.Id, contact.Name, contact.Phone, (ConsentStatusType)contact.ConsentStatusId,
				contact.ConsentRequestedAt, contact.ConsentRespondedAt);
	}
}
