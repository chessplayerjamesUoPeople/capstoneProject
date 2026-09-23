using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a status log entity in the application.
	/// Each time a user's status changes, a new entry is created in this log to keep track of the change.
	/// </summary>
	public class StatusLog
	{
		/// <summary>
		/// The unique identifier for the status log entry.
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The unique identifier of the user whose status has changed.
		/// Foreign key referencing the User entity.
		/// </summary>
		public Guid UserId { get; set; }

		/// <summary>
		/// The date and time when the status change occurred. This is required and cannot be null.
		/// </summary>
		public DateTimeOffset TimeOccurrence { get; set; }

		/// <summary>
		/// The identifier for the new status of the user. This is a foreign key that references the Status entity.
		/// </summary>
		public byte StatusId { get; set; }

		/// <summary>
		/// The geographical location where the status change occurred. This is optional and can be null.
		/// </summary>
		public Point? Location { get; set; }

		/// <summary>
		/// Additional notes or comments about the status change. This is optional and can be null.
		/// </summary>
		public string? Notes { get; set; }

		//Navigation properties

		/// <summary>
		/// The User this status log entry belongs to.
		/// </summary>
		public User User { get; set; } = null!;

		/// <summary>
		/// The Status of this log entry.
		/// </summary>
		public Status Status { get; set; } = null!;
	}
}
