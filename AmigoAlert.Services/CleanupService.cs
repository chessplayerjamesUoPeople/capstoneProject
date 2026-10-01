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
		private readonly IStatusLogRepository _statusLogRepository;

		/// <summary>
		/// Initializes a new instance of the <see cref="CleanupService"/> class.
		/// </summary>
		/// <param name="statusLogRepository">The repository used for managing status log entries.</param>
		public CleanupService(IStatusLogRepository statusLogRepository)
		{
			_statusLogRepository = statusLogRepository;
		}

		/// <inheritdoc/>
		public async Task<int> CleanupOldLocations()
		{
			return await _statusLogRepository.DeleteOldLocationLogs();
		}
	}
}
