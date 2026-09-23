using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a consent status entity in the application.
	/// This is the current state of consent that an emergency contact has given.
	/// Seeded by <see cref="Shared.Enums.ConsentStatusType"/>
	/// </summary>
	public class ConsentStatus
	{
		/// <summary>
		/// The unique identifier for the consent status.
		/// Purposely byte, as only a few (less than 10) consent statuses are expected.
		/// </summary>
		public byte Id { get; set; }

		/// <summary>
		/// The name of the consent status.
		/// </summary>
		public string name { get; set; } = null!;

		//Navigation property

		/// <summary>
		/// The emergency contacts associated with this consent status.
		/// </summary>
		public ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
	}
}
