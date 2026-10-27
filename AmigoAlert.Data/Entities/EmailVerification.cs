using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Entities
{
	/// <summary>
	/// Represents a one-time code emailed to an address, to prove the person using the app can read that inbox. Only a salted hash of the
	/// code is stored.
	/// </summary>
	/// <remarks>
	/// Not linked to a user: a registration code is issued before the user exists. Rows are short-lived and purged by the cleanup job.
	/// </remarks>
	public class EmailVerification
	{
		/// <summary>
		/// The unique identifier for the verification.
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The address the code was sent to, trimmed and lower-cased.
		/// </summary>
		public string Email { get; set; } = null!;

		/// <summary>
		/// What the code was issued for, represented by the unique identifier of the verification purpose.
		/// This is a foreign key that references the VerificationPurpose entity.
		/// </summary>
		public byte PurposeId { get; set; }

		/// <summary>
		/// The SHA-256 hash of <see cref="CodeSalt"/> followed by the code's UTF-8 bytes.
		/// </summary>
		public byte[] CodeHash { get; set; } = null!;

		/// <summary>
		/// The random salt hashed with the code.
		/// </summary>
		public byte[] CodeSalt { get; set; } = null!;

		/// <summary>
		/// The date and time the code was issued.
		/// </summary>
		public DateTimeOffset CreatedAt { get; set; }

		/// <summary>
		/// The date and time after which the code can no longer be used. Brought forward to the time of issue of a newer code, which
		/// supersedes this one.
		/// </summary>
		public DateTimeOffset ExpiresAt { get; set; }

		/// <summary>
		/// The number of incorrect codes entered against this verification. Once it reaches the service's limit, the code is unusable.
		/// </summary>
		public byte FailedAttempts { get; set; }

		/// <summary>
		/// The date and time the code was used, or null if it has not been.
		/// </summary>
		public DateTimeOffset? ConsumedAt { get; set; }

		//Navigation Properties

		/// <summary>
		/// What the code was issued for.
		/// </summary>
		public VerificationPurpose Purpose { get; set; } = null!;
	}
}
