using System.Globalization;
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

		/// <summary>Covers the location lookup and the upload, inside the time FCM gives a message handler.</summary>
		private static readonly TimeSpan LocationReportTimeout = TimeSpan.FromSeconds(12);

		/// <summary>Short, so a slow network cannot hold up the location report that follows it.</summary>
		private static readonly TimeSpan DeliveryReportTimeout = TimeSpan.FromSeconds(5);

		public override void OnMessageReceived(RemoteMessage message)
		{
			// Before anything else, so it is the moment the message arrived and not when the notification was drawn.
			var receivedAt = DateTimeOffset.UtcNow;

			base.OnMessageReceived(message);

			if (!CheckInNotifications.ShowCheckIn(this, message.Data))
			{
				Log.Warn(LogTag, "Ignored a push that is not a well-formed check-in.");
				return;
			}

			// Both after the notification is posted, so neither can delay it.
			ReportDelivery(message.Data, receivedAt);
			ReportLocation(message.Data);
		}

		/// <summary>
		/// Tells the API when the check-in arrived, so the system knows its notifications are reaching the device and how long they take.
		/// Best effort and never throws: a report lost to a dropped connection only leaves the delivery time unknown.
		/// </summary>
		private static void ReportDelivery(IDictionary<string, string> data, DateTimeOffset receivedAt)
		{
			try
			{
				if (!data.TryGetValue("checkInId", out var id) || !long.TryParse(id, CultureInfo.InvariantCulture, out var checkInId))
					return;

				var api = IPlatformApplication.Current?.Services.GetRequiredService<AmigoAlertApiClient>()
					?? throw new InvalidOperationException("The MAUI application has not been created.");

				// Waited for here, like the location report, so the process is not reclaimed mid-request.
				using var timeout = new CancellationTokenSource(DeliveryReportTimeout);
				api.ReportCheckInDeliveryAsync(checkInId, receivedAt, timeout.Token).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Log.Warn(LogTag, $"Reporting the delivery failed: {ex.GetType().Name}: {ex.Message}");
			}
		}

		/// <summary>
		/// Sends the device location for the check-in just shown, so that if the user does not answer it, the alert to their emergency
		/// contact can say where they last were. Runs for the reminder too, so the newest fix is the one used. Best effort and never throws.
		/// </summary>
		/// <remarks>
		/// Waits here, on the thread FCM delivers the message on (not the UI thread), instead of starting a task: FCM allows about twenty
		/// seconds, and the process could otherwise be reclaimed mid-upload.
		/// </remarks>
		private static void ReportLocation(IDictionary<string, string> data)
		{
			try
			{
				if (!data.TryGetValue("checkInId", out var id) || !long.TryParse(id, CultureInfo.InvariantCulture, out var checkInId))
					return;

				var api = IPlatformApplication.Current?.Services.GetRequiredService<AmigoAlertApiClient>()
					?? throw new InvalidOperationException("The MAUI application has not been created.");

				using var timeout = new CancellationTokenSource(LocationReportTimeout);
				api.ReportCheckInLocationAsync(checkInId, timeout.Token).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				// A 404 or 409 only means the check-in is no longer waiting for an answer. The location itself is never logged.
				Log.Warn(LogTag, $"Reporting the location failed: {ex.GetType().Name}: {ex.Message}");
			}
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
				// Nobody has registered or logged in on this device yet; doing so calls this again through ApiSettings.UserIdChanged.
				if (await ApiSettings.GetUserIdAsync() is not { } userId)
					return;

				token ??= (await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.Object>())?.ToString();
				if (string.IsNullOrEmpty(token))
					return;

				var api = IPlatformApplication.Current?.Services.GetRequiredService<AmigoAlertApiClient>()
					?? throw new InvalidOperationException("The MAUI application has not been created.");

				using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await api.RegisterDeviceAsync(userId,GetInstallationId(), new RegisterDeviceDto(token), timeout.Token);
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
