using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <inheritdoc cref="ICheckInRepository"/>
	internal sealed class CheckInRepository(AmigoAlertDbContext context) : ICheckInRepository
	{
		/// <summary>
		/// Ecuador observes UTC-5 year-round, without daylight saving time adjustments.
		/// </summary>
		private static readonly TimeSpan EcuadorOffset = TimeSpan.FromHours(-5);

		/// <inheritdoc cref="ICheckInRepository.AddAsync"/>
		public async Task AddAsync(CheckIn checkIn, CancellationToken ct = default)
		{
			context.CheckIns.Add(checkIn);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetByIdAsync"/>
		public Task<CheckIn?> GetByIdAsync(long checkInId, CancellationToken ct = default)
		{
			return context.CheckIns.FirstOrDefaultAsync(c => c.Id == checkInId, ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetForUserOnDateAsync"/>
		public Task<CheckIn?> GetForUserOnDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
		{
			DateTimeOffset dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), EcuadorOffset);
			DateTimeOffset dayEnd = dayStart.AddDays(1);

			return context.CheckIns
				.Where(c => c.UserId == userId)
				.Where(c => c.ScheduledFor >= dayStart && c.ScheduledFor < dayEnd)
				.OrderByDescending(c => c.ScheduledFor)
				.ThenByDescending(c => c.Id)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetForUserSinceAsync"/>
		public async Task<IReadOnlyList<CheckIn>> GetForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default)
		{
			return await context.CheckIns
				.Where(c => c.UserId == userId && c.ScheduledFor >= since)
				.OrderByDescending(c => c.ScheduledFor)
				.ThenByDescending(c => c.Id)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetLatestForUserAsync"/>
		public Task<CheckIn?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default)
		{
			return context.CheckIns
				.Where(c => c.UserId == userId)
				.OrderByDescending(c => c.ScheduledFor)
				.ThenByDescending(c => c.Id)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetLatestForUsersAsync"/>
		public async Task<IReadOnlyList<CheckIn>> GetLatestForUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
		{
			IReadOnlyCollection<Guid> ids = userIds as IReadOnlyCollection<Guid> ?? userIds.ToList();
			if (ids.Count == 0)
			{
				return [];
			}

			// The Id tiebreak picks the reminder row over the first notification when both share ScheduledFor; without it the query would
			// return both and the caller's ToDictionary would throw on the duplicate user.
			return await context.CheckIns
				.Where(c => ids.Contains(c.UserId))
				.GroupBy(c => c.UserId)
				.Select(g => g.OrderByDescending(c => c.ScheduledFor).ThenByDescending(c => c.Id).First())
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.RecordResponseAsync"/>
		public Task<int> RecordResponseAsync(long checkInId, CheckInResponseType response, DateTimeOffset receivedAt,
			CancellationToken ct = default)
		{
			IQueryable<CheckIn> answered = context.CheckIns.Where(c => c.Id == checkInId);

			return context.CheckIns
				.Where(c => c.TimeReceived == null)
				.Where(c => answered.Any(a => a.UserId == c.UserId && a.ScheduledFor == c.ScheduledFor))
				.ExecuteUpdateAsync(s => s
					.SetProperty(c => c.TimeReceived, receivedAt)
					.SetProperty(c => c.ResponseId, (byte)response), ct);
		}

		/// <inheritdoc cref="ICheckInRepository.RecordDeliveryAsync"/>
		public Task<int> RecordDeliveryAsync(long checkInId, DateTimeOffset deliveredAt, CancellationToken ct = default)
		{
			return context.CheckIns
				.Where(c => c.Id == checkInId && c.TimeDelivered == null)
				.ExecuteUpdateAsync(s => s.SetProperty(c => c.TimeDelivered, deliveredAt), ct);
		}
	}
}
