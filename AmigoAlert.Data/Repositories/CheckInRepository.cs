using AmigoAlert.Data.Entities;
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
		
		/// <inheritdoc cref="ICheckInRepository.GetByIdAsync"/>
		public Task<CheckIn?> GetByIdAsync(long checkInId, CancellationToken ct = default)
		{
			return context.CheckIns
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.Id == checkInId, ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetForUserOnDateAsync"/>
		public Task<CheckIn?> GetForUserOnDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
		{
			DateTimeOffset dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), EcuadorOffset);
			DateTimeOffset dayEnd = dayStart.AddDays(1);

			return context.CheckIns
				.AsNoTracking()
				.Where(c => c.UserId == userId)
				.Where(c => c.ScheduledFor >= dayStart && c.ScheduledFor < dayEnd)
				.OrderByDescending(c => c.ScheduledFor)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetForUserSinceAsync"/>
		public async Task<IReadOnlyList<CheckIn>> GetForUserSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default)
		{
			return await context.CheckIns
				.AsNoTracking()
				.Where(c => c.UserId == userId && c.ScheduledFor >= since)
				.OrderByDescending(c => c.ScheduledFor)
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="ICheckInRepository.GetLatestForUserAsync"/>
		public Task<CheckIn?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default)
		{
			return context.CheckIns
				.AsNoTracking()
				.Where(c => c.UserId == userId)
				.OrderByDescending(c => c.ScheduledFor)
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

			var latest = context.CheckIns
				.Where(c => ids.Contains(c.UserId))
				.GroupBy(c => c.UserId)
				.Select(g => new { UserId = g.Key, ScheduledFor = g.Max(x => x.ScheduledFor) });

			return await context.CheckIns
				.AsNoTracking()
				.Where(c => latest.Any(l => l.UserId == c.UserId && l.ScheduledFor == c.ScheduledFor))
				.ToListAsync(ct);
		}
	}
}
