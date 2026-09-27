using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Registers new users. The user and their initial status log row are written together, so every user starts with a recorded state
	/// rather than an implicit one.
	/// </summary>
	public sealed class RegistrationService(IUserRepository userRepository, ITransactionProvider transactionProvider,
		TimeProvider timeProvider)
	{
		/// <summary>
		/// Registers <paramref name="user"/>, assigning its identifier, its initial status of <see cref="StatusType.Okay"/> and its terms
		/// acceptance time.
		/// </summary>
		/// <param name="user">The user to register, populated from the request. <see cref="User.Id"/> is set on return.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the user was registered; false when the email address is already registered.</returns>
		public async Task<bool> RegisterAsync(User user, CancellationToken ct = default)
		{
			if (await userRepository.EmailExistsAsync(user.Email, ct))
			{
				return false;
			}

			DateTimeOffset now = timeProvider.GetUtcNow();

			user.Id = Guid.NewGuid();
			user.StatusId = (byte)StatusType.Okay;
			user.AcceptedTermsTime = now;

			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			await userRepository.AddAsync(user, ct);
			await userRepository.AddStatusLogAsync(new StatusLog
			{
				UserId = user.Id,
				TimeOccurrence = now,
				StatusId = (byte)StatusType.Okay,
				Notes = "Registered."
			}, ct);
			await transaction.CommitAsync(ct);

			return true;
		}
	}
}
