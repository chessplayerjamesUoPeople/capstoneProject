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

		/// <summary>
		/// Deletes email verification codes that expired more than a day before <paramref name="now"/>, used or not.
		/// </summary>
		/// <param name="now">The current time.</param>
		/// <returns>The number of verification codes that were deleted.</returns>
		public Task<int> CleanupExpiredVerificationCodes(DateTimeOffset now);
	}
}
