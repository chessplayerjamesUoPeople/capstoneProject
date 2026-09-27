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
		/// <summary>
		/// Twilio statuses after which a message's outcome no longer changes.
		/// </summary>
		private static readonly string[] FinalStatuses = ["delivered", "undelivered", "failed"];

		/// <inheritdoc cref="ISmsRepository.AddAsync"/>
		public async Task AddAsync(Sms message, CancellationToken ct = default)
		{
			context.Sms.Add(message);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetByIdAsync"/>
		public Task<Sms?> GetByIdAsync(long smsId, CancellationToken ct = default)
		{
			return context.Sms.FirstOrDefaultAsync(s => s.Id == smsId, ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetByProviderMessageIdAsync"/>
		public Task<Sms?> GetByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default)
		{
			return context.Sms.FirstOrDefaultAsync(s => s.TwilioSid == providerMessageId, ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetForUserSinceAsync"/>
		public async Task<IReadOnlyList<Sms>> GetForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default)
		{
			return await context.Sms
				.Where(s => s.EmergencyContact.UserId == userId && s.TimeSent >= since)
				.OrderByDescending(s => s.TimeSent)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ISmsRepository.GetPendingDeliveryAsync"/>
		public async Task<IReadOnlyList<Sms>> GetPendingDeliveryAsync(DateTimeOffset olderThan, CancellationToken ct = default)
		{
			return await context.Sms
				.Where(s => s.TimeSent < olderThan)
				.Where(s => s.Status == null && s.ErrorCode == null)
				.OrderBy(s => s.TimeSent)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ISmsRepository.RecordDeliveryStatusAsync"/>
		public Task<int> RecordDeliveryStatusAsync(string providerMessageId, string status, int? errorCode, CancellationToken ct = default)
		{
			return context.Sms
				.Where(s => s.TwilioSid == providerMessageId)
				.Where(s => s.Status == null || !FinalStatuses.Contains(s.Status))
				.ExecuteUpdateAsync(s => s
					.SetProperty(m => m.Status, status)
					.SetProperty(m => m.ErrorCode, errorCode), ct);
		}
	}
}
