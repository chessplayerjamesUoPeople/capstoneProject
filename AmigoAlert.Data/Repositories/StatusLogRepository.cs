using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Implements the <see cref="IStatusLogRepository"/> interface for managing status log entries in the data store.
	/// </summary>
	internal sealed class StatusLogRepository : IStatusLogRepository
	{
		private readonly AmigoAlertDbContext _dbContext;

		/// <summary>
		/// Initializes a new instance of the <see cref="StatusLogRepository"/> class.
		/// </summary>
		/// <param name="dbContext">The database context used for data access.</param>
		public StatusLogRepository(AmigoAlertDbContext dbContext)
		{
			_dbContext = dbContext;
		}

		/// <inheritdoc/>
		public async Task<int> DeleteOldLocationLogs()
		{
			DateTimeOffset cutoffDate = DateTimeOffset.UtcNow.AddDays(-90);
			List<StatusLog> oldLogs = await _dbContext.StatusLogs
				.Where(log => log.Location != null && log.TimeOccurrence < cutoffDate)
				.ToListAsync();
			foreach (StatusLog log in oldLogs)
			{
				log.Location = null;
			}

			_dbContext.StatusLogs.UpdateRange(oldLogs);
			await _dbContext.SaveChangesAsync();
			return oldLogs.Count;
		}
	}
}
