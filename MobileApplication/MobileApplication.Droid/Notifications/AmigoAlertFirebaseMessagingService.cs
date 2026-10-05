using AmigoAlert.Shared.DTOs;
using Android.App;
using Android.Gms.Extensions;
using Android.Util;
using Firebase.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MobileApplication.Services;

namespace MobileApplication.Droid.Notifications
{
	/// <summary>
	/// Receives check-in pushes and FCM token changes. Check-ins are data-only messages, so this runs whether the app is in the
	/// foreground, in the background or not running, and the notification it posts always carries the two actions.
	/// </summary>
	[Service(Exported = false)]
	[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
	public sealed class AmigoAlertFirebaseMessagingService : FirebaseMessagingService
	{
		private const string LogTag = "AmigoAlert.Push";
		private const string InstallationIdKey = "push_installation_id";

		public override void OnMessageReceived(RemoteMessage message)
		{
			base.OnMessageReceived(message);

			if (!CheckInNotifications.ShowCheckIn(this, message.Data))
				Log.Warn(LogTag, "Ignored a push that is not a well-formed check-in.");
		}

		// Firebase Messaging 25.1 deprecated the token APIs in favour of Firebase installation IDs, but Notification Hubs still addresses
		// FCM v1 devices by registration token.
#pragma warning disable CS0618, CS0672
		public override void OnNewToken(string token)
		{
			base.OnNewToken(token);
			_ = RegisterAsync(token);
		}

		/// <summary>
		/// Registers this device with the API for the current user's check-ins. Never throws: a failure is logged, and the next launch tries
		/// again.
		/// </summary>
		/// <param name="token">A token FCM has just issued, or null to ask FCM for the current one.</param>
		public static async Task RegisterAsync(string? token = null)
		{
			try
			{
				// Throws when the build had no google-services.json, so Firebase was never initialised.
				token ??= (await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.Object>())?.ToString();
				if (string.IsNullOrEmpty(token))
					return;

				var api = IPlatformApplication.Current?.Services.GetRequiredService<AmigoAlertApiClient>()
					?? throw new InvalidOperationException("The MAUI application has not been created.");

				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await api.RegisterDeviceAsync(ApiSettings.CurrentUserId, GetInstallationId(), new RegisterDeviceDto(token), timeout.Token);
			}
			catch (Exception ex)
			{
				// The token identifies the device, so it is left out of the log.
				Log.Warn(LogTag, $"Push registration failed: {ex.GetType().Name}: {ex.Message}");
			}
		}
#pragma warning restore CS0618, CS0672

		/// <summary>
		/// The identifier of this installation, created on first use and kept for the life of the install, so registering again replaces
		/// this device's registration instead of adding another. Not a secret.
		/// </summary>
		private static Guid GetInstallationId()
		{
			if (Guid.TryParse(Preferences.Default.Get(InstallationIdKey, string.Empty), out var installationId))
				return installationId;

			installationId = Guid.NewGuid();
			Preferences.Default.Set(InstallationIdKey, installationId.ToString());

			return installationId;
		}
	}
}
