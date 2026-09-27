using AmigoAlert.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing SMS-related data operations.
	/// </summary>
	/// <remarks>
	/// Beginning with GET operations only.
	/// ******************************************************************************
	/// TODO: Implement POST and PUT operations for SMS management in the future.
	/// ******************************************************************************
	/// </remarks>
	public interface ISmsRepository
	{
		/// <summary>
		/// Retrieves an SMS entity by its unique identifier.
		/// </summary>
		/// <param name="smsId">The unique identifier of the SMS entity to retrieve.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The SMS entity with the specified unique identifier, or null if no such entity exists.</returns>
		Task<Sms?> GetByIdAsync(long smsId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves an SMS entity by its provider message identifier.
		/// </summary>
		/// <param name="providerMessageId">The provider message identifier of the SMS entity to retrieve.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The SMS entity with the specified provider message identifier, or null if no such entity exists.</returns>
		Task<Sms?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves all SMS entities for a specific user since a specified date and time.
		/// </summary>
		/// <param name="userId">The unique identifier of the user whose SMS entities are to be retrieved.</param>
		/// <param name="since">The date and time since when to retrieve the SMS entities.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of SMS entities for the specified user since the specified date and time.</returns>
		Task<IReadOnlyList<Sms>> GetForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default);

		/// <summary>
		/// Retrieves all SMS entities that are pending delivery and older than a specified date and time.
		/// </summary>
		/// <param name="olderThan">The date and time before which the SMS entities are considered pending delivery.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of SMS entities that are pending delivery and older than the specified date and time.</returns>
		Task<IReadOnlyList<Sms>> GetPendingDeliveryAsync(DateTimeOffset olderThan, CancellationToken ct = default);
	}
}
