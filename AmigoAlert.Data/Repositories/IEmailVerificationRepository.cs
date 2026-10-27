using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing the one-time codes emailed to prove ownership of an address.
	/// </summary>
	/// <remarks>
	/// Email addresses are matched exactly; callers pass them already trimmed and lower-cased. Update methods return the affected row
	/// count, so a caller can tell "updated" from "no such row" in one round trip.
	/// </remarks>
	public interface IEmailVerificationRepository
	{
		/// <summary>
		/// Inserts a new verification. <see cref="EmailVerification.Id"/> is populated on return.
		/// </summary>
		/// <param name="verification">The verification to insert.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		Task AddAsync(EmailVerification verification, CancellationToken ct = default);

		/// <summary>
		/// Retrieves the newest verification for the address and purpose that is unused and has not expired at <paramref name="now"/>.
		/// </summary>
		/// <param name="email">The normalized email address.</param>
		/// <param name="purpose">What the code was issued for.</param>
		/// <param name="now">The current time.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The newest usable verification, or null if there is none.</returns>
		Task<EmailVerification?> GetLatestActiveAsync(string email, VerificationPurposeType purpose, DateTimeOffset now,
			CancellationToken ct = default);

		/// <summary>
		/// Retrieves the issue times of every code sent to the address for the purpose since <paramref name="since"/>, used to limit how
		/// often codes can be requested.
		/// </summary>
		/// <param name="email">The normalized email address.</param>
		/// <param name="purpose">What the codes were issued for.</param>
		/// <param name="since">The earliest issue time to include.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The issue times, oldest first.</returns>
		Task<IReadOnlyList<DateTimeOffset>> GetIssueTimesSinceAsync(string email, VerificationPurposeType purpose, DateTimeOffset since,
			CancellationToken ct = default);

		/// <summary>
		/// Records one incorrect code entered against a verification.
		/// </summary>
		/// <param name="verificationId">The unique identifier of the verification.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such verification exists.</returns>
		Task<int> IncrementFailedAttemptsAsync(long verificationId, CancellationToken ct = default);

		/// <summary>
		/// Marks a verification as used, only if it is still unused and unexpired at <paramref name="now"/>. Of two concurrent attempts to
		/// use the same code, exactly one succeeds.
		/// </summary>
		/// <param name="verificationId">The unique identifier of the verification.</param>
		/// <param name="now">The current time, recorded as the time of use.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if the verification was already used, has expired or does not exist.</returns>
		Task<int> ConsumeAsync(long verificationId, DateTimeOffset now, CancellationToken ct = default);

		/// <summary>
		/// Expires every unused, unexpired code for the address and purpose, so that only a code issued after this call can be used.
		/// </summary>
		/// <param name="email">The normalized email address.</param>
		/// <param name="purpose">What the codes were issued for.</param>
		/// <param name="now">The current time, which becomes their expiry time.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated.</returns>
		Task<int> ExpireActiveAsync(string email, VerificationPurposeType purpose, DateTimeOffset now, CancellationToken ct = default);

		/// <summary>
		/// Deletes every verification that expired before <paramref name="before"/>, whether or not it was used.
		/// </summary>
		/// <param name="before">The cutoff expiry time.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows deleted.</returns>
		Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken ct = default);
	}
}
