using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <inheritdoc cref="IEmailVerificationRepository"/>
	internal sealed class EmailVerificationRepository(AmigoAlertDbContext context) : IEmailVerificationRepository
	{
		/// <inheritdoc cref="IEmailVerificationRepository.AddAsync"/>
		public async Task AddAsync(EmailVerification verification, CancellationToken ct = default)
		{
			context.EmailVerifications.Add(verification);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.ConsumeAsync"/>
		public Task<int> ConsumeAsync(long verificationId, DateTimeOffset now, CancellationToken ct = default)
		{
			return context.EmailVerifications
				.Where(e => e.Id == verificationId && e.ConsumedAt == null && e.ExpiresAt > now)
				.ExecuteUpdateAsync(s => s.SetProperty(e => e.ConsumedAt, now), ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.DeleteExpiredAsync"/>
		public Task<int> DeleteExpiredAsync(DateTimeOffset before, CancellationToken ct = default)
		{
			return context.EmailVerifications.Where(e => e.ExpiresAt < before).ExecuteDeleteAsync(ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.ExpireActiveAsync"/>
		public Task<int> ExpireActiveAsync(string email, VerificationPurposeType purpose, DateTimeOffset now, CancellationToken ct = default)
		{
			return Active(email, purpose, now).ExecuteUpdateAsync(s => s.SetProperty(e => e.ExpiresAt, now), ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.GetIssueTimesSinceAsync"/>
		public async Task<IReadOnlyList<DateTimeOffset>> GetIssueTimesSinceAsync(string email, VerificationPurposeType purpose,
			DateTimeOffset since, CancellationToken ct = default)
		{
			byte purposeId = (byte)purpose;

			return await context.EmailVerifications
				.Where(e => e.Email == email && e.PurposeId == purposeId && e.CreatedAt >= since)
				.OrderBy(e => e.CreatedAt)
				.Select(e => e.CreatedAt)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.GetLatestActiveAsync"/>
		public Task<EmailVerification?> GetLatestActiveAsync(string email, VerificationPurposeType purpose, DateTimeOffset now,
			CancellationToken ct = default)
		{
			return Active(email, purpose, now)
				.OrderByDescending(e => e.CreatedAt)
				.ThenByDescending(e => e.Id)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="IEmailVerificationRepository.IncrementFailedAttemptsAsync"/>
		public Task<int> IncrementFailedAttemptsAsync(long verificationId, CancellationToken ct = default)
		{
			return context.EmailVerifications
				.Where(e => e.Id == verificationId)
				.ExecuteUpdateAsync(s => s.SetProperty(e => e.FailedAttempts, e => (byte)(e.FailedAttempts + 1)), ct);
		}

		private IQueryable<EmailVerification> Active(string email, VerificationPurposeType purpose, DateTimeOffset now)
		{
			byte purposeId = (byte)purpose;

			return context.EmailVerifications.Where(e => e.Email == email && e.PurposeId == purposeId && e.ConsumedAt == null
				&& e.ExpiresAt > now);
		}
	}
}
