using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Represents a repository interface for managing emergency contacts and their consent state.
	/// </summary>
	/// <remarks>
	/// Contacts are never hard-deleted: the record that consent was once granted to a number is required for LOPDP audit, so replacing a
	/// contact deactivates the old row. A filtered unique index permits exactly one active contact per user.
	/// </remarks>
	public interface IEmergencyContactRepository
	{
		/// <summary>
		/// Retrieves the user's active emergency contact.
		/// </summary>
		/// <param name="userId">The GUID ID of the user whose emergency contact is to be retrieved.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The active emergency contact for the user, or null if the user has none.</returns>
		Task<EmergencyContact?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

		/// <summary>
		/// Retrieves the active emergency contact holding the specified consent token, with <see cref="EmergencyContact.User"/> loaded.
		/// </summary>
		/// <param name="token">The consent token from the link sent to the contact.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The emergency contact holding the token, or null if no active contact holds it.</returns>
		Task<EmergencyContact?> GetByConsentTokenAsync(Guid token, CancellationToken ct = default);

		/// <summary>
		/// Inserts a new emergency contact. The caller assigns <see cref="EmergencyContact.Id"/> and the consent token.
		/// </summary>
		/// <param name="contact">The emergency contact to insert.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		Task AddAsync(EmergencyContact contact, CancellationToken ct = default);

		/// <summary>
		/// Renames an active emergency contact. The phone number, and therefore the consent, is unchanged.
		/// </summary>
		/// <param name="contactId">The GUID ID of the contact to rename.</param>
		/// <param name="name">The contact's new full name.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such active contact exists.</returns>
		Task<int> UpdateNameAsync(Guid contactId, string name, CancellationToken ct = default);

		/// <summary>
		/// Deactivates an emergency contact and clears any outstanding consent token, so a pending link can no longer be used. The row is
		/// kept for audit.
		/// </summary>
		/// <param name="contactId">The GUID ID of the contact to deactivate.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if no such active contact exists.</returns>
		Task<int> DeactivateAsync(Guid contactId, CancellationToken ct = default);

		/// <summary>
		/// Records the contact's answer to a consent request and clears the token so the link cannot be reused. Applies only while the
		/// contact is active, still pending, and the token has not expired at <paramref name="respondedAt"/>, so a repeated or late
		/// submission records nothing.
		/// </summary>
		/// <param name="contactId">The GUID ID of the contact who responded.</param>
		/// <param name="status">The resulting consent status: granted or rejected.</param>
		/// <param name="respondedAt">The time the response was received.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The number of rows updated: 1, or 0 if the request was no longer open.</returns>
		Task<int> RecordConsentResponseAsync(Guid contactId, ConsentStatusType status, DateTimeOffset respondedAt,
			CancellationToken ct = default);
	}
}
