using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a type of SMS message that can be sent to an emergency contact.
	/// </summary>
	/// <remarks>
	/// *********************************************************************************
	/// TODO:	An enum needs to be created to represent the possible SMS Message Types.
	///			This will help in maintaining consistency and clarity in the codebase.
	///	*********************************************************************************
	///</remarks>
	public class SmsMessageType
	{
		/// <summary>
		/// The unique identifier for the SMS message type.
		/// The value is a byte, as only a few (less than 10) message types are expected.
		/// </summary>
		public byte Id { get; set; }

		/// <summary>
		/// The name of the SMS message type.
		/// </summary>
		public string Name { get; set; } = null!;

		//Navigation Property

		/// <summary>
		/// The collection of SMS messages of this message type.
		/// </summary>
		public ICollection<Sms> SmsMessages { get; set; } = new List<Sms>();
	}
}
