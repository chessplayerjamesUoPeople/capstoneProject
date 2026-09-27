using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <inheritdoc cref="IUserRepository"/>
	internal sealed class UserRepository(AmigoAlertDbContext context) : IUserRepository
	{
		/// <inheritdoc cref="IUserRepository.AddAsync"/>
		public async Task AddAsync(User user, CancellationToken ct = default)
		{
			context.Users.Add(user);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.AddStatusLogAsync"/>
		public async Task AddStatusLogAsync(StatusLog entry, CancellationToken ct = default)
		{
			context.StatusLogs.Add(entry);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.EmailExistsAsync"/>
		public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
		{
			return context.Users.AnyAsync(u => u.Email == email, ct);
		}

		/// <inheritdoc cref="IUserRepository.ExistsAsync"/>
		public Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users.AnyAsync(u => u.Id == userId, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetActiveUsersWithContactsAsync"/>
		public async Task<IReadOnlyList<User>> GetActiveUsersWithContactsAsync(CancellationToken ct = default)
		{
			return await context.Users
				.Where(u => u.EmergencyContacts.Any(c => c.IsActive && c.ConsentStatusId == (byte)ConsentStatusType.Granted))
				.Include(u => u.EmergencyContacts.Where(c => c.IsActive))
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.GetByIdAsync"/>
		public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetLatestStatusAsync"/>
		public Task<StatusLog?> GetLatestStatusAsync(Guid userId, CancellationToken ct = default)
		{
			return context.StatusLogs
				.Where(sl => sl.UserId == userId)
				.OrderByDescending(sl => sl.TimeOccurrence)
				.ThenByDescending(sl => sl.Id)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.GetWithEmergencyContactAsync"/>
		public Task<User?> GetWithEmergencyContactAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users
				.Include(u => u.EmergencyContacts.Where(ec => ec.IsActive))
				.FirstOrDefaultAsync(u => u.Id == userId, ct);
		}

		/// <inheritdoc cref="IUserRepository.TransitionStatusAsync"/>
		public Task<int> TransitionStatusAsync(Guid userId, IReadOnlyCollection<StatusType> from, StatusType to,
			CancellationToken ct = default)
		{
			byte[] fromIds = from.Select(s => (byte)s).ToArray();

			return context.Users
				.Where(u => u.Id == userId && fromIds.Contains(u.StatusId))
				.ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, (byte)to), ct);
		}

		/// <inheritdoc cref="IUserRepository.UpdateProfileAsync"/>
		public Task<int> UpdateProfileAsync(Guid userId, string name, string? phone, string address, string? landmarks,
			CancellationToken ct = default)
		{
			return context.Users
				.Where(u => u.Id == userId)
				.ExecuteUpdateAsync(s => s
					.SetProperty(u => u.Name, name)
					.SetProperty(u => u.Phone, phone)
					.SetProperty(u => u.Address, address)
					.SetProperty(u => u.Landmarks, landmarks), ct);
		}

		/// <inheritdoc cref="IUserRepository.UpdateStatusAsync"/>
		public Task<int> UpdateStatusAsync(Guid userId, StatusType status, CancellationToken ct = default)
		{
			return context.Users
				.Where(u => u.Id == userId)
				.ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, (byte)status), ct);
		}
	}
}
