using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a check-in event/notification sent to the user.
	/// </summary>
	public class CheckIn
	{
		/// <summary>
		/// The unique identifier for the check-in event.
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The unique identifier of the user associated with this check-in event.
		/// This is the user that the check-in notification was sent to.
		/// </summary>
		public Guid UserId { get; set; }
		
		/// <summary>
		/// The date and time when the check-in is scheduled to occur.
		/// </summary>
		public DateTimeOffset ScheduledFor { get; set; }

		/// <summary>
		/// The date and time when the check-in notification was sent to the user.
		/// </summary>
		public DateTimeOffset TimeSent { get; set; }

		/// <summary>
		/// The date and time when the user responded to the check-in notification.
		/// </summary>
		public DateTimeOffset? TimeReceived { get; set; }
		
		/// <summary>
		/// The unique identifier of the response associated with this check-in event.
		/// </summary>
		public byte ResponseId { get; set; }

		//Navigation Properties

		/// <summary>
		/// The User the check-in notification was sent to.
		/// </summary>
		public User User { get; set; } = null!;

		/// <summary>
		/// The response the user provided to the check-in notification.
		/// </summary>
		public CheckInResponse Response { get; set; } = null!;
	}
}
