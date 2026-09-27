using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a user entity in the application.
	/// </summary>
	public class User
	{
		/// <summary>
		/// The unique identifier for the user.
		/// </summary>
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// The name of the user. This includes first and last name.
		/// </summary>
		public string Name { get; set; } = null!;

		/// <summary>
		/// The email address of the user. This should be unique.
		/// </summary>
		public string Email { get; set; } = null!;

		/// <summary>
		/// The mobile phone number of the user. This is optional and can be null.
		/// Expected format is E.164 (e.g., +593991234567).
		/// </summary>
		public string? Phone { get; set; }

		/// <summary>
		/// The physical address of the user (where they live). This is required and cannot be null.
		/// The expected format includes street address, city, state/province, and country
		/// (e.g., "1234 Sunset Av., Cotacachi, Ecuador").
		/// </summary>
		public string Address { get; set; } = null!;

		/// <summary>
		/// A description of nearby buildings, landmarks, or other notable features to help locate the user's residence.
		/// This field is optional.
		/// </summary>
		public string? Landmarks { get; set; }

		/// <summary>
		/// The identifier for the user's status. This is a foreign key that references the Status entity.
		/// </summary>
		public byte StatusId { get; set; }

		/// <summary>
		/// The date and time when the user accepted the terms and conditions. This is required and cannot be null.
		/// </summary>
		public DateTimeOffset AcceptedTermsTime { get; set; }

		/// <summary>
		/// The version of the terms and conditions that the user accepted. This is required and cannot be null.
		/// When the current version of terms and conditions differes from the accepted,
		/// the user will be prompted to accept the new version.
		/// </summary>
		public string AcceptedTermsVersion { get; set; } = null!;

		/// <summary>
		/// The time of day when the first notification should be sent to the user. This is optional and can be null.
		/// TODO: This functionality is not implemented for the MVP. It is planned for future releases.
		/// </summary>
		public TimeOnly? Notification1Time { get; set; }

		/// <summary>
		/// The time of day when the second notification should be sent to the user. This is optional and can be null.
		/// TODO: This functionality is not implemented for the MVP. It is planned for future releases.
		/// </summary>
		public TimeOnly? Notification2Time { get; set; }

		//Navigation properties

		/// <summary>
		/// The current status of the user.
		/// </summary>
		public Status Status { get; set; } = null!;

		/// <summary>
		/// The collection of emergency contacts associated with the user.
		/// </summary>
		public ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();

		/// <summary>
		/// The collection of status logs associated with the user.
		/// Each time the user's status changes, a new entry is created in this log to keep track of the change.
		/// </summary>
		public ICollection<StatusLog> StatusLogs { get; set; } = new List<StatusLog>();

		/// <summary>
		/// The collection of check-in events/notifications sent to the user.
		/// Each check-in represents a notification sent to the user, and the user's response (if any) is recorded.
		/// </summary>
		public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
	}
}
