using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing check-in-related data operations.
	/// </summary>
	/// <remarks>
	/// A reminder creates a second row sharing the first notification's <see cref="CheckIn.ScheduledFor"/>, so one occurrence can have two
	/// rows. "Latest" queries order by <see cref="CheckIn.ScheduledFor"/> and then by <see cref="CheckIn.Id"/> to pick the newer of the
	/// two.
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
		/// Retrieves the latest check-in record for each of a list of users, at most one per user.
		/// </summary>
		/// <param name="userIds">A collection of unique identifiers of the users whose latest check-in records are to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of the latest check-in records for the specified users.</returns>
		Task<IReadOnlyList<CheckIn>> GetLatestForUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

		/// <summary>
		/// Inserts a check-in row recording that a notification was sent. <see cref="CheckIn.Id"/> is populated on return.
		/// </summary>
		/// <param name="checkIn">The check-in to insert.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		Task AddAsync(CheckIn checkIn, CancellationToken ct = default);

		/// <summary>
		/// Records the user's answer against every unanswered row of the occurrence <paramref name="checkInId"/> belongs to, so answering
		/// the first notification after the reminder was sent still marks the reminder row, which is the one the escalation rules read.
		/// </summary>
		/// <remarks>
		/// Conditional on <see cref="CheckIn.TimeReceived"/> being null: a second answer, or one racing the WebJob, updates nothing.
		/// </remarks>
		/// <param name="checkInId">The unique identifier of the check-in the user answered.</param>
		/// <param name="response">The user's response.</param>
		/// <param name="receivedAt">The time the response was received.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated, or 0 if the occurrence does not exist or was already answered.</returns>
		Task<int> RecordResponseAsync(long checkInId, CheckInResponseType response, DateTimeOffset receivedAt,
			CancellationToken ct = default);
	}
}
