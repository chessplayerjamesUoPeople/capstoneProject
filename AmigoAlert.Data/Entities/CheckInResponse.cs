using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a response to a check-in event/notification sent to the user.
	/// </summary>
	/// <remarks>
	/// *******************************************************************************
	/// TODO:	An enum needs to be created to represent the possible responses.
	///			This will help in maintaining consistency and clarity in the codebase.
	///	*******************************************************************************
	///</remarks>
	public class CheckInResponse
	{
		/// <summary>
		/// The unique identifier for the check-in response.
		/// Purposely byte, as only a few (less than 10) responses are expected.
		/// </summary>
		public byte Id { get; set; }

		/// <summary>
		/// The name of the check-in response.
		/// </summary>
		public string Name { get; set; } = null!;
	}
}
