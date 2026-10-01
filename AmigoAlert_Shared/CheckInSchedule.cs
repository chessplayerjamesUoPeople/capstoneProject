using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared
{
	/// <summary>
	/// Works out when a user's next check-in falls, so the mobile client can show the time remaining until it.
	/// </summary>
	public static class CheckInSchedule
	{
		/// <summary>Ecuador (mainland) observes UTC-5 year-round with no daylight saving; the check-in times are local to it.</summary>
		public static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-5);

		/// <summary>
		/// The earliest configured check-in instant strictly after <paramref name="now"/>.
		/// </summary>
		/// <param name="first">The first daily check-in time, in Ecuador local time, or null.</param>
		/// <param name="second">The second daily check-in time, in Ecuador local time, or null.</param>
		/// <param name="now">The instant to look forward from.</param>
		/// <returns>The next check-in, or null when neither time is configured.</returns>
		public static DateTimeOffset? NextOccurrence(TimeOnly? first, TimeOnly? second, DateTimeOffset now)
		{
			DateTimeOffset localNow = now.ToOffset(LocalOffset);
			DateTimeOffset? best = null;

			for (int dayShift = 0; dayShift <= 1; dayShift++)
			{
				DateOnly date = DateOnly.FromDateTime(localNow.Date).AddDays(dayShift);

				foreach (TimeOnly? time in new[] { first, second })
				{
					if (time is null)
					{
						continue;
					}

					DateTimeOffset candidate = new DateTimeOffset(date.ToDateTime(time.Value), LocalOffset);

					if (candidate > now && (best is null || candidate < best))
					{
						best = candidate;
					}
				}
			}

			return best;
		}
	}
}
