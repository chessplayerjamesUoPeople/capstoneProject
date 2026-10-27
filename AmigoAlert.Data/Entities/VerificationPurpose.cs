using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents what an email verification code was issued for.
	/// Seeded by <see cref="Shared.Enums.VerificationPurposeType"/>
	/// </summary>
	public class VerificationPurpose
	{
		/// <summary>
		/// The unique identifier for the purpose.
		/// Purposely byte, as only a few (less than 10) purposes are expected.
		/// </summary>
		public byte Id { get; set; }

		/// <summary>
		/// The name of the purpose.
		/// </summary>
		public string Name { get; set; } = null!;
	}
}
