using AmigoAlert.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing user-related data operations.
	/// </summary>
	/// <remarks>
	/// Beginning with GET operations only.
	/// ***************************************************************************
	/// TODO: Implement POST and PUT operations for user management in the future.
	/// ***************************************************************************
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
		/// Asynchronously retrieves the emergency contact associated with a user by their ID.
		/// </summary>
		/// <param name="userId">The GUID ID of the user whose emergency contact is to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The emergency contact associated with the specified user ID, or null if no such contact exists.</returns>
		Task<EmergencyContact?> GetEmergencyContactAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Asynchronously retrieves the emergency contact associated with a user by their consent token.
		/// </summary>
		/// <param name="token">The consent token of the user whose emergency contact is to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The emergency contact associated with the specified consent token, or null if no such contact exists.</returns>
		Task<EmergencyContact?> GetEmergencyContactByConsentTokenAsync(string token, CancellationToken ct = default);

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
	}
}
