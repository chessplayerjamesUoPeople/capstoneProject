using AmigoAlert.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Represents a service responsible for performing cleanup operations within the application.
	/// </summary>
	public sealed class CleanupService : ICleanupService
	{
		/// <summary>
		/// How long a verification row is kept after it expires. Long enough to cover <see cref="EmailVerificationService"/>'s hourly request
		/// limit, which counts these rows.
		/// </summary>
		public static readonly TimeSpan VerificationRetention = TimeSpan.FromHours(24);

		private readonly IStatusLogRepository _statusLogRepository;
		private readonly IEmailVerificationRepository _emailVerificationRepository;

		/// <summary>
		/// Initializes a new instance of the <see cref="CleanupService"/> class.
		/// </summary>
		/// <param name="statusLogRepository">The repository used for managing status log entries.</param>
		/// <param name="emailVerificationRepository">The repository used for managing email verification codes.</param>
		public CleanupService(IStatusLogRepository statusLogRepository, IEmailVerificationRepository emailVerificationRepository)
		{
			_statusLogRepository = statusLogRepository;
			_emailVerificationRepository = emailVerificationRepository;
		}

		/// <inheritdoc/>
		public async Task<int> CleanupOldLocations()
		{
			return await _statusLogRepository.DeleteOldLocationLogs();
		}

		/// <inheritdoc/>
		public async Task<int> CleanupExpiredVerificationCodes(DateTimeOffset now)
		{
			return await _emailVerificationRepository.DeleteExpiredAsync(now - VerificationRetention);
		}
	}
}
