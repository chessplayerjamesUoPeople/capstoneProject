using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <inheritdoc cref="ISmsRepository"/>
	internal sealed class SmsRepository(AmigoAlertDbContext context) : ISmsRepository
	{
		/// <inheritdoc cref="ISmsRepository.GetByIdAsync"/>
		public Task<Sms?> GetByIdAsync(long smsId, CancellationToken ct = default)
		{
			return context.Sms
				.AsNoTracking()
				.FirstOrDefaultAsync(s => s.Id == smsId, ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetByProviderMessageIdAsync"/>
		public Task<Sms?> GetByProviderMessageIdAsync(
			string providerMessageId, CancellationToken ct = default)
		{
			return context.Sms
				.AsNoTracking()
				.FirstOrDefaultAsync(s => s.TwilioSid == providerMessageId, ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetForUserSinceAsync"/>
		public async Task<IReadOnlyList<Sms>> GetForUserSinceAsync(
			Guid userId, DateTimeOffset since, CancellationToken ct = default)
		{
			return await context.Sms
				.AsNoTracking()
				.Where(s => s.EmergencyContact.UserId == userId && s.TimeSent >= since)
				.OrderByDescending(s => s.TimeSent)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetPendingDeliveryAsync"/>
		public async Task<IReadOnlyList<Sms>> GetPendingDeliveryAsync(
			DateTimeOffset olderThan, CancellationToken ct = default)
		{
			return await context.Sms
				.AsNoTracking()
				.Where(s => s.TimeSent < olderThan)
				.Where(s => s.Status == null && s.ErrorCode == null)
				.OrderBy(s => s.TimeSent)
				.ToListAsync(ct);
		}
	}
}
