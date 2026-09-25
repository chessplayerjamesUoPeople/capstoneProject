using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents an SMS message sent to an emergency contact.
	/// </summary>
	public class Sms
	{
		/// <summary>
		/// The unique identifier for the SMS message.
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// The unique identifier of the emergency contact to whom the SMS message was sent.
		/// </summary>
		public Guid EmergencyContactId { get; set; }
		
		/// <summary>
		/// The unique identifier of the status log associated with the SMS message.
		/// </summary>
		public long? StatusLogId { get; set; }
		
		/// <summary>
		/// The date and time when the SMS message was sent.
		/// </summary>
		public DateTimeOffset TimeSent { get; set; }

		/// <summary>
		/// The unique identifier provided by Twilio for the SMS message.
		/// This is used to track the message status and delivery.
		/// </summary>
		public string? TwilioSid { get; set; }

		/// <summary>
		/// The status of the SMS message as reported by Twilio.
		/// </summary>
		public string? Status { get; set; }

		/// <summary>
		/// The error code provided by Twilio if the SMS message failed to send.
		/// </summary>
		public int? ErrorCode { get; set; }

		/// <summary>
		/// The type of message sent, represented by the unique identifier of the SMS message type.
		/// This is a foreign key that references the SmsMessageType entity.
		/// </summary>
		public byte MessageTypeId { get; set; }

		//Navigation Properties

		/// <summary>
		/// The Emergency Contact the SMS message was sent to.
		/// </summary>
		public EmergencyContact EmergencyContact { get; set; } = null!;

		/// <summary>
		/// The Status Log the SMS message is associated with. This is optional and can be null.
		/// </summary>
		public StatusLog? StatusLog { get; set; }

		/// <summary>
		/// The type of message sent.
		/// </summary>
		public SmsMessageType MessageType { get; set; } = null!;
	}
}
