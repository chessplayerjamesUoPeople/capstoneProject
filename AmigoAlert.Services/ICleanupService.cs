using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Defines a service responsible for performing cleanup operations within the application.
	/// </summary>
	public interface ICleanupService
	{
		/// <summary>
		/// Cleans up old location data (older than 90 days) from the system.
		/// </summary>
		/// <returns>The number of location records that were deleted.</returns>
		public Task<int> CleanupOldLocations();
	}
}
