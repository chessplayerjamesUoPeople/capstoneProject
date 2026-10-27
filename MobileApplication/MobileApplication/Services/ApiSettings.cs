namespace MobileApplication.Services
{
	/// <summary>
	/// Where the app finds the AmigoAlert API, and which user it acts for (the ID kept in secure storage after registration).
	/// </summary>
	public static class ApiSettings
	{
		/// <summary>The deployed AmigoAlert API. HTTPS only (NF-12).</summary>
		public const string DeployedBaseAddress = "https://amigoalertapi-c3hqbah0affjcpa7.westus-01.azurewebsites.net/";

#if DEBUG
		/// <summary>True to use the API running on this PC (the "https" launch profile) instead of the deployed one.</summary>
		public const bool UseLocalApi = false;

		/// <summary>
		/// The API running on this PC. The Android emulator reaches the PC's localhost at 10.0.2.2; its own localhost is the emulator.
		/// </summary>
		public static string LocalBaseAddress =>
			DeviceInfo.Platform == DevicePlatform.Android ? "https://192.168.1.2:7012/" : "https://localhost:7012/"; //https://10.0.2.2:7012/

		public static string BaseAddress => UseLocalApi ? LocalBaseAddress : DeployedBaseAddress;
#else
		public static string BaseAddress => DeployedBaseAddress;
#endif

		/// <summary>The version of the Terms and Conditions the user accepts when registering.</summary>
		public const string TermsVersion = "1.0";

		/// <summary>The default daily check-in times, in Ecuador local time (which is how the API stores them).</summary>
		public static readonly TimeOnly FirstNotificationTime = new(8, 0);
		public static readonly TimeOnly SecondNotificationTime = new(18, 0);

		private const string UserIdKey = "user_id";

		private static Guid? cachedUserId;

		/// <summary>
		/// The user's ID from secure storage, or null when nobody has registered or logged in on this device, or the app's data was
		/// cleared. A value that cannot be read (for example after a restore to another device, when the encryption key is gone) is
		/// discarded. Either way the user logs in again to recover their account.
		/// </summary>
		public static async Task<Guid?> GetUserIdAsync()
		{
			if (cachedUserId is not null)
				return cachedUserId;

			try
			{
				if (Guid.TryParse(await SecureStorage.Default.GetAsync(UserIdKey), out var userId))
					return cachedUserId = userId;
			}
			catch (Exception)
			{
				SecureStorage.Default.Remove(UserIdKey);
			}

			return null;
		}

		/// <summary>
		/// Raised after <see cref="SetUserIdAsync"/> stores a user, so the platform can register this device for that user's check-in
		/// notifications straight away rather than on the next launch.
		/// </summary>
		public static event EventHandler? UserIdChanged;

		/// <summary>Remembers the user returned by registration or log in, in secure storage.</summary>
		public static async Task SetUserIdAsync(Guid userId)
		{
			await SecureStorage.Default.SetAsync(UserIdKey, userId.ToString());
			cachedUserId = userId;
			UserIdChanged?.Invoke(null, EventArgs.Empty);
		}
	}
}
