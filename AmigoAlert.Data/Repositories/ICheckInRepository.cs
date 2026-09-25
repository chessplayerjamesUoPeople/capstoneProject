using AmigoAlert.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing check-in-related data operations.
	/// </summary>
	/// <remarks>
	/// Beginning with GET operations only.
	/// ***************************************************************************
	/// TODO: Implement POST and PUT operations for user management in the future.
	/// ***************************************************************************
	/// </remarks>
	public interface ICheckInRepository
	{
		/// <summary>
		/// Retrieves a check-in record by its unique identifier.
		/// </summary>
		/// <param name="checkInId">The unique identifier of the check-in record to retrieve.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The check-in record with the specified unique identifier, or null if no such record exists.</returns>
		Task<CheckIn?> GetByIdAsync(long checkInId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves the latest check-in record for a specific user.
		/// </summary>
		/// <param name="userId">The unique identifier of the user whose latest check-in record is to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The latest check-in record for the specified user, or null if no such record exists.</returns>
		Task<CheckIn?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves a check-in record for a specific user on a specific date.
		/// </summary>
		/// <param name="userId">The unique identifier of the user whose check-in record is to be retrieved.</param>
		/// <param name="date">The date for which to retrieve the check-in record.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The check-in record for the specified user on the specified date, or null if no such record exists.</returns>
		Task<CheckIn?> GetForUserOnDateAsync(Guid userId, DateOnly date, CancellationToken ct = default);

		/// <summary>
		/// Retrieves all check-in records for a specific user since a specified date and time.
		/// </summary>
		/// <param name="userId">The unique identifier of the user whose check-in records are to be retrieved.</param>
		/// <param name="since">The date and time since when to retrieve the check-in records.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of check-in records for the specified user since the specified date and time.</returns>
		Task<IReadOnlyList<CheckIn>> GetForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default);

		/// <summary>
		/// Retrieves the latest check-in records for a list of users.
		/// </summary>
		/// <param name="userIds">A collection of unique identifiers of the users whose latest check-in records are to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of the latest check-in records for the specified users.</returns>
		Task<IReadOnlyList<CheckIn>> GetLatestForUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
	}
}
