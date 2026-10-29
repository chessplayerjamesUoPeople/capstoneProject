using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Registers new users. The user and their initial status log row are written together, so every user starts with a recorded state
	/// rather than an implicit one. Registration requires a code emailed by <see cref="EmailVerificationService"/>, proving the user owns
	/// the address they will later log in with.
	/// </summary>
	public sealed class RegistrationService(IUserRepository userRepository, EmailVerificationService verificationService,
		ITransactionProvider transactionProvider, TimeProvider timeProvider)
	{
		/// <summary>
		/// Registers <paramref name="user"/>, assigning its identifier, its initial status of <see cref="StatusType.Okay"/> and its terms
		/// acceptance time.
		/// </summary>
		/// <param name="user">The user to register, populated from the request. <see cref="User.Id"/> is set on return.</param>
		/// <param name="verificationCode">The registration code emailed to <see cref="User.Email"/>.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the user was registered, and if not, why.</returns>
		public async Task<RegistrationOutcome> RegisterAsync(User user, string verificationCode, CancellationToken ct = default)
		{
			if (await userRepository.EmailExistsAsync(user.Email, ct))
			{
				return RegistrationOutcome.EmailAlreadyRegistered;
			}

			if (await verificationService.MatchCodeAsync(user.Email, VerificationPurposeType.Registration, verificationCode, ct)
				is not { } verificationId)
			{
				return RegistrationOutcome.InvalidVerificationCode;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();

			user.Id = Guid.NewGuid();
			user.StatusId = (byte)StatusType.Okay;
			user.AcceptedTermsTime = now;

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			// Used up with the insert, so the code still works if the insert fails, and a concurrent second submission registers nothing.
			if (!await verificationService.ConsumeAsync(verificationId, ct))
			{
				return RegistrationOutcome.InvalidVerificationCode;
			}

			await userRepository.AddAsync(user, ct);
			await userRepository.AddStatusLogAsync(new StatusLog
			{
				UserId = user.Id,
				TimeOccurrence = now,
				StatusId = (byte)StatusType.Okay,
				Notes = "Registered."
			}, ct);
			await transaction.CommitAsync(ct);

			return RegistrationOutcome.Registered;
		}
	}
}
