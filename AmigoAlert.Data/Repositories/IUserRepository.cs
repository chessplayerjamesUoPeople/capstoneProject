using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing user-related data operations, covering the user and their status log. Emergency
	/// contacts have their own repository, <see cref="IEmergencyContactRepository"/>.
	/// </summary>
	/// <remarks>
	/// Update methods return the affected row count, so a caller can tell "updated" from "no such row" in one round trip.
	/// </remarks>
	public interface IUserRepository
	{
		/// <summary>
		/// Asynchronously retrieves a user by their ID.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to retrieve.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The user with the specified ID, or null if no such user exists.</returns>
		Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously retrieves a user along with their associated emergency contact by their ID.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to retrieve along with their emergency contact.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The user with the specified ID and their associated emergency contact, or null if no such user exists.</returns>
		Task<User?> GetWithEmergencyContactAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Determines whether a user with the specified ID exists in the repository.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to check for existence.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True if a user with the specified ID exists; otherwise, false.</returns>
		Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Determines whether a user is already registered with the specified email address.
		/// </summary>
		/// <param name="email">The email address to check. Comparison follows the database collation (case-insensitive).</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True if the email address is already registered; otherwise, false.</returns>
		Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

		/// <summary>
		/// Retrieves the latest status log entry for a user by their ID.
		/// </summary>
		/// <param name="userId">The GUID ID of the user whose latest status log entry is to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The latest status log entry for the specified user ID, or null if no such entry exists.</returns>
		Task<StatusLog?> GetLatestStatusAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves a list of all active users along with their associated emergency contacts.
		/// </summary>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A read-only list of all active users with their associated emergency contacts.</returns>
		Task<IReadOnlyList<User>> GetActiveUsersWithContactsAsync(CancellationToken ct = default);

		/// <summary>
		/// Inserts a new user. The caller assigns <see cref="User.Id"/>.
		/// </summary>
		/// <param name="user">The user to insert.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		Task AddAsync(User user, CancellationToken ct = default);

		/// <summary>
		/// Updates the user's editable profile fields.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to update.</param>
		/// <param name="name">The user's full name.</param>
		/// <param name="phone">The user's mobile number in E.164 format, or null.</param>
		/// <param name="address">The user's physical address.</param>
		/// <param name="landmarks">Nearby landmarks that help locate the residence, or null.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such user exists.</returns>
		Task<int> UpdateProfileAsync(Guid userId, string name, string? phone, string address, string? landmarks,
			CancellationToken ct = default);

		/// <summary>
		/// Sets the user's current status unconditionally.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to update.</param>
		/// <param name="status">The new status.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such user exists.</returns>
		Task<int> UpdateStatusAsync(Guid userId, StatusType status, CancellationToken ct = default);

		/// <summary>
		/// Sets the user's current status only when it is currently one of <paramref name="from"/>. This guards writes that act on an
		/// earlier evaluation, so that, for example, a check-in answered in the meantime is not overwritten by an escalation.
		/// </summary>
		/// <param name="userId">The GUID ID of the user to update.</param>
		/// <param name="from">The statuses the user must currently hold for the update to apply.</param>
		/// <param name="to">The new status.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such user exists or their status was not one of
		/// <paramref name="from"/>.</returns>
		Task<int> TransitionStatusAsync(Guid userId, IReadOnlyCollection<StatusType> from, StatusType to, CancellationToken ct = default);

		/// <summary>
		/// Inserts an audit row recording a status change. <see cref="StatusLog.Id"/> is populated on return.
		/// </summary>
		/// <param name="entry">The status log entry to insert.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		Task AddStatusLogAsync(StatusLog entry, CancellationToken ct = default);
	}
}
