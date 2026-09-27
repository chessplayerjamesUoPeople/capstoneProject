using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <inheritdoc cref="IEmergencyContactRepository"/>
	internal sealed class EmergencyContactRepository(AmigoAlertDbContext context) : IEmergencyContactRepository
	{
		/// <inheritdoc cref="IEmergencyContactRepository.AddAsync"/>
		public async Task AddAsync(EmergencyContact contact, CancellationToken ct = default)
		{
			context.EmergencyContacts.Add(contact);
			await context.SaveChangesAsync(ct);
		}

		/// <inheritdoc cref="IEmergencyContactRepository.DeactivateAsync"/>
		public Task<int> DeactivateAsync(Guid contactId, CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.Where(c => c.Id == contactId && c.IsActive)
				.ExecuteUpdateAsync(s => s
					.SetProperty(c => c.IsActive, false)
					.SetProperty(c => c.ConsentToken, (Guid?)null)
					.SetProperty(c => c.ConsentTokenExpires, (DateTimeOffset?)null), ct);
		}

		/// <inheritdoc cref="IEmergencyContactRepository.GetByConsentTokenAsync"/>
		public Task<EmergencyContact?> GetByConsentTokenAsync(Guid token, CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.Include(c => c.User)
				.FirstOrDefaultAsync(c => c.ConsentToken == token && c.IsActive, ct);
		}

		/// <inheritdoc cref="IEmergencyContactRepository.GetByUserIdAsync"/>
		public Task<EmergencyContact?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
		{
			return context.EmergencyContacts.FirstOrDefaultAsync(c => c.UserId == userId && c.IsActive, ct);
		}

		/// <inheritdoc cref="IEmergencyContactRepository.RecordConsentResponseAsync"/>
		public Task<int> RecordConsentResponseAsync(Guid contactId, ConsentStatusType status, DateTimeOffset respondedAt,
			CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.Where(c => c.Id == contactId && c.IsActive && c.ConsentStatusId == (byte)ConsentStatusType.Pending)
				.Where(c => c.ConsentTokenExpires > respondedAt)
				.ExecuteUpdateAsync(s => s
					.SetProperty(c => c.ConsentStatusId, (byte)status)
					.SetProperty(c => c.ConsentRespondedAt, respondedAt)
					.SetProperty(c => c.ConsentToken, (Guid?)null)
					.SetProperty(c => c.ConsentTokenExpires, (DateTimeOffset?)null), ct);
		}

		/// <inheritdoc cref="IEmergencyContactRepository.UpdateNameAsync"/>
		public Task<int> UpdateNameAsync(Guid contactId, string name, CancellationToken ct = default)
		{
			return context.EmergencyContacts
				.Where(c => c.Id == contactId && c.IsActive)
				.ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, name), ct);
		}
	}
}
