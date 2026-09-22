using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a status entity in the application: what is the user's status?
	/// </summary>
	internal class Status
	{
		/// <summary>
		/// The unique identifier for the status. Purposely byte, as only a few (less than 10) statuses are expected.
		/// </summary>
		public byte Id { get; set; }

		/// <summary>
		/// The name of the status.
		/// </summary>
		public string Name { get; set; } = null!;
	}
}
