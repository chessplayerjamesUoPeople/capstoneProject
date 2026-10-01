namespace MobileApplication.Services
{
	/// <summary>
	/// Where the app finds the AmigoAlert API, and which user it acts for.
	/// </summary>
	public static class ApiSettings
	{
		/// <summary>The deployed AmigoAlert API. HTTPS only (NF-12).</summary>
		public const string DeployedBaseAddress = "https://amigoalertapi-c3hqbah0affjcpa7.westus-01.azurewebsites.net/";

#if DEBUG
		/// <summary>True to use the API running on this PC (the "https" launch profile) instead of the deployed one.</summary>
		public const bool UseLocalApi = true;

		/// <summary>
		/// The API running on this PC. The Android emulator reaches the PC's localhost at 10.0.2.2; its own localhost is the emulator.
		/// </summary>
		public static string LocalBaseAddress =>
			DeviceInfo.Platform == DevicePlatform.Android ? "https://10.0.2.2:7012/" : "https://localhost:7012/";

		public static string BaseAddress => UseLocalApi ? LocalBaseAddress : DeployedBaseAddress;
#else
		public static string BaseAddress => DeployedBaseAddress;
#endif

		/// <summary>The signed-in user.</summary>
		// TODO: Load from secure storage once registration returns the new user's ID.
		public static readonly Guid CurrentUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
	}
}
