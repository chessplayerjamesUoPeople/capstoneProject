namespace MobileApplication.Services
{
	/// <summary>
	/// Where the app finds the AmigoAlert API, and which user it acts for.
	/// </summary>
	public static class ApiSettings
	{
		/// <summary>The deployed AmigoAlert API. HTTPS only (NF-12).</summary>
		public const string BaseAddress = "https://amigoalertapi-c3hqbah0affjcpa7.westus-01.azurewebsites.net/";

		/// <summary>The signed-in user.</summary>
		// TODO: Load from secure storage once registration returns the new user's ID.
		public static readonly Guid CurrentUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
	}
}
