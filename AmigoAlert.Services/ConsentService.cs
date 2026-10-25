using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Orchestrates emergency contact consent under LOPDP Articles 7 and 8: requesting consent when a contact is added or their number
	/// changes, and recording the contact's answer.
	/// </summary>
	/// <remarks>
	/// Consent attaches to a phone number, not a person. A new number has agreed to nothing, so changing it deactivates the old contact
	/// (never deleting it, since the record of past consent is needed for audit) and creates a new one at pending consent. The consent SMS
	/// is dispatched inside the transaction, which commits only if the provider accepted it, so a failed request leaves the previous
	/// contact in place and can simply be repeated.
	/// </remarks>
	public sealed class ConsentService(IUserRepository userRepository, IEmergencyContactRepository contactRepository,
		ISmsService smsService, ITransactionProvider transactionProvider, IOptions<ConsentOptions> options, TimeProvider timeProvider)
	{
		/// <summary>
		/// How long a consent link remains usable. Long enough for an older contact to notice the message over a weekend; after this the
		/// user must re-submit the contact to send a new link.
		/// </summary>
		public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

		/// <summary>
		/// Adds the user's first emergency contact and sends it a consent request.
		/// </summary>
		/// <param name="userId">The user the contact belongs to.</param>
		/// <param name="name">The contact's full name.</param>
		/// <param name="phone">The contact's number in E.164 format.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The new contact, or why it could not be added.</returns>
		public Task<EmergencyContactChangeResult> AddContactAsync(Guid userId, string name, string phone, CancellationToken ct = default)
		{
			return SetContactAsync(userId, name, phone, replaceExisting: false, ct);
		}

		/// <summary>
		/// Replaces the user's emergency contact. A changed phone number invalidates consent and starts a new request; a name-only change
		/// keeps the existing contact and its consent.
		/// </summary>
		/// <param name="userId">The user the contact belongs to.</param>
		/// <param name="name">The contact's full name.</param>
		/// <param name="phone">The contact's number in E.164 format.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The resulting active contact, or why it could not be changed.</returns>
		public Task<EmergencyContactChangeResult> ReplaceContactAsync(Guid userId, string name, string phone,
			CancellationToken ct = default)
		{
			return SetContactAsync(userId, name, phone, replaceExisting: true, ct);
		}

		/// <summary>
		/// Looks up the consent request behind a link so the contact can see whose notifications they are being asked to receive.
		/// </summary>
		/// <param name="token">The consent token from the link.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The request, or null when no active contact holds the token.</returns>
		public async Task<ConsentRequestDto?> GetRequestAsync(Guid token, CancellationToken ct = default)
		{
			EmergencyContact? contact = await contactRepository.GetByConsentTokenAsync(token, ct);
			if (contact is null)
			{
				return null;
			}

			ConsentStatusType status = (ConsentStatusType)contact.ConsentStatusId;
			bool canRespond = status == ConsentStatusType.Pending && contact.ConsentTokenExpires > timeProvider.GetUtcNow();

			return new ConsentRequestDto(contact.Name, contact.User.Name, status, canRespond);
		}

		/// <summary>
		/// Records a contact's answer to a consent request. Missing, used, superseded and expired tokens are indistinguishable to the
		/// caller: the endpoint is unauthenticated, and saying which one applies would tell a guesser more than it needs to know.
		/// </summary>
		/// <param name="token">The consent token from the link.</param>
		/// <param name="granted">True to grant consent, false to reject it.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the answer was recorded; false when the request was not open.</returns>
		public async Task<bool> RespondAsync(Guid token, bool granted, CancellationToken ct = default)
		{
			EmergencyContact? contact = await contactRepository.GetByConsentTokenAsync(token, ct);
			DateTimeOffset now = timeProvider.GetUtcNow();

			if (contact is null || !contact.IsActive || contact.ConsentStatusId != (byte)ConsentStatusType.Pending
				|| !(contact.ConsentTokenExpires > now))
			{
				return false;
			}

			ConsentStatusType status = granted ? ConsentStatusType.Granted : ConsentStatusType.Rejected;

			// The update repeats these conditions in its predicate, so a concurrent second submission records nothing.
			return await contactRepository.RecordConsentResponseAsync(contact.Id, status, now, ct) > 0;
		}

		private async Task<EmergencyContactChangeResult> SetContactAsync(Guid userId, string name, string phone, bool replaceExisting,
			CancellationToken ct)
		{
			User? user = await userRepository.GetByIdAsync(userId, ct);
			if (user is null)
			{
				return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.UserNotFound, null);
			}

			EmergencyContact? existing = await contactRepository.GetByUserIdAsync(userId, ct);

			if (existing is null && replaceExisting)
			{
				return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.ContactNotFound, null);
			}

			if (existing is not null && !replaceExisting)
			{
				return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.ContactAlreadyExists, null);
			}

			if (existing is not null && string.Equals(existing.Phone, phone, StringComparison.Ordinal))
			{
				await contactRepository.UpdateNameAsync(existing.Id, name, ct);
				existing.Name = name;

				return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.NameUpdated, existing);
			}

			DateTimeOffset now = timeProvider.GetUtcNow();
			Guid token = Guid.NewGuid();

			EmergencyContact contact = new EmergencyContact
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				Name = name,
				Phone = phone,
				ConsentStatusId = (byte)ConsentStatusType.Pending,
				ConsentToken = token,
				ConsentTokenExpires = now + TokenLifetime,
				ConsentRequestedAt = now,
				IsActive = true
			};

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			// The filtered unique index permits one active contact per user, so deactivation must precede the insert.
			if (existing is not null)
			{
				await contactRepository.DeactivateAsync(existing.Id, ct);
			}

			await contactRepository.AddAsync(contact, ct);

			foreach (string body in SmsMessageComposer.ConsentRequestMessages(user.Name, options.Value.PageBaseUrl + token.ToString("N")))
			{
				SmsDispatchResult dispatch = await smsService.SendAsync(contact.Id, contact.Phone, body, SmsMessageTypeEnum.ConsentRequest,
					null, ct);

				if (!dispatch.Succeeded)
				{
					return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.SmsFailed, null);
				}
			}

			await transaction.CommitAsync(ct);

			return new EmergencyContactChangeResult(EmergencyContactChangeOutcome.ConsentRequested, contact);
		}
	}
}
