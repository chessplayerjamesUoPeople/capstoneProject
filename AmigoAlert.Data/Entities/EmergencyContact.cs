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
		/// The unique identifier for the emergency contact. Assigned with Guid.NewGuid() in service code, not here, so EF materialization
		/// does not generate a discarded value.
		/// </summary>
		public Guid Id { get; set; }

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
		public byte ConsentStatusId { get; set; }

		/// <summary>
		/// The unique token generated for the consent request.
		/// This is used to create a URL the emergency contact can click to provide their consent. It stays valid for as long as the
		/// contact is active, so the same link lets the contact withdraw consent later; it is cleared only when the contact is deactivated.
		/// </summary>
		public Guid? ConsentToken { get; set; }

		/// <summary>
		/// The date and time when the pending consent request expires. Applies only while the status is pending and is cleared once the
		/// contact responds.
		/// </summary>
		public DateTimeOffset? ConsentTokenExpires { get; set; }

		/// <summary>
		/// The date and time when the consent was requested from the emergency contact.
		/// </summary>
		public DateTimeOffset ConsentRequestedAt { get; set; }

		/// <summary>
		/// The date and time of the emergency contact's latest consent response: granting, rejecting, withdrawing or granting again.
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

		/// <summary>
		/// The collection of SMS messages sent to this emergency contact.
		/// </summary>
		public ICollection<Sms> SmsMessages { get; set; } = new List<Sms>();
	}
}
