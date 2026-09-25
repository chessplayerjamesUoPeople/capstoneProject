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
		/// <inheritdoc cref="IUserRepository.ExistsAsync"/>
		public Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users.AnyAsync(u => u.Id == userId, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetActiveUsersWithContactsAsync"/>
		public async Task<IReadOnlyList<User>> GetActiveUsersWithContactsAsync(CancellationToken ct = default)
		{
			return await context.Users
				.AsNoTracking()
				.Where(u => u.EmergencyContacts.Any(c => c.IsActive && c.ConsentStatusId == (byte)ConsentStatusType.Granted))
				.Include(u => u.EmergencyContacts.Where(c => c.IsActive))
				.ToListAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.GetByIdAsync"/>
		public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(u => u.Id == userId, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetEmergencyContactAsync"/>
		public Task<EmergencyContact?> GetEmergencyContactAsync(Guid userId, CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.AsNoTracking()
				.FirstOrDefaultAsync(ec => ec.UserId == userId && ec.IsActive, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetEmergencyContactByConsentTokenAsync"/>
		public Task<EmergencyContact?> GetEmergencyContactByConsentTokenAsync(string token, CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.AsNoTracking()
				.FirstOrDefaultAsync(ec => ec.ConsentToken.ToString() == token && ec.IsActive, ct);
		}

		/// <inheritdoc cref="IUserRepository.GetLatestStatusAsync"/>
		public Task<StatusLog?> GetLatestStatusAsync(Guid userId, CancellationToken ct = default)
		{
			return context.StatusLogs
				.AsNoTracking()
				.Where(sl => sl.UserId == userId)
				.OrderByDescending(sl => sl.TimeOccurrence)
				.FirstOrDefaultAsync(ct);
		}

		/// <inheritdoc cref="IUserRepository.GetWithEmergencyContactAsync"/>
		public Task<User?> GetWithEmergencyContactAsync(Guid userId, CancellationToken ct = default)
		{
			return context.Users
				.AsNoTracking()
				.Include(u => u.EmergencyContacts.Where(ec => ec.IsActive))
				.FirstOrDefaultAsync(u => u.Id == userId, ct);
		}
	}
}
