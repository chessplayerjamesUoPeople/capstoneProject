using AmigoAlert.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Repositories
{
	/// <summary>
	/// Defines a repository interface for managing status log entries in the data store.
	/// </summary>
	public interface IStatusLogRepository
	{
		/// <summary>
		/// Deletes old status logs from the data store that have a location record.
		/// This only deletes logs older than 90 days.
		/// </summary>
		/// <returns>The number of status logs deleted.</returns>
		public Task<int> DeleteOldLocationLogs();
	}
}
