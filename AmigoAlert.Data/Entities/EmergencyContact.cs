using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents an emergency contact entity in the application.
	/// Users can add emergency contacts to their profile, which can be used in case of emergencies.
	/// </summary>
	public class EmergencyContact
	{
		/// <summary>
		/// The unique identifier for the emergency contact.
		/// </summary>
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// The unique identifier of the user to whom this emergency contact belongs.
		/// Foreign key referencing the User entity.
		/// </summary>
		public Guid UserId { get; set; }

		/// <summary>
		/// The name of the emergency contact. This includes first and last name.
		/// </summary>
		public string Name { get; set; } = null!;

		/// <summary>
		/// The phone number of the emergency contact. This is required and cannot be null.
		/// The expected format is E.164 (e.g., +593991234567).
		/// </summary>
		public string Phone { get; set; } = null!;

		/// <summary>
		/// The identifier for the consent status. This is a foreign key that references the ConsentStatus entity.
		/// </summary>
		/// <remarks>
		/// ******************************************************************************
		/// TODO: An enum needs to be created to represent the possible consent statuses.
		/// ******************************************************************************
		/// </remarks>
		public byte ConsentStatusId { get; set; }

		/// <summary>
		/// The unique token generated for the consent request.
		/// This is used to create a URL the emergency contact can click to provide their consent.
		/// </summary>
		public Guid? ConsentToken { get; set; } = Guid.NewGuid();

		/// <summary>
		/// The date and time when the consent token expires.
		/// </summary>
		public DateTimeOffset? ConsentTokenExpires { get; set; }

		/// <summary>
		/// The date and time when the consent was requested from the emergency contact.
		/// </summary>
		public DateTimeOffset ConsentRequestedAt { get; set; }

		/// <summary>
		/// The date and time when the consent was responded to by the emergency contact.
		/// </summary>
		public DateTimeOffset? ConsentRespondedAt { get; set; }

		/// <summary>
		/// Indicates whether the emergency contact is active.
		/// </summary>
		public bool IsActive { get; set; } = true;

		//Navigation properties

		/// <summary>
		/// The User this emergency contact will receive alerts for.
		/// </summary>
		public User User { get; set; } = null!;

		/// <summary>
		/// The consent status of the emergency contact.
		/// </summary>
		public ConsentStatus ConsentStatus { get; set; } = null!;
	}
}
