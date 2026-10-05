using Android.Content;
using Android.Util;
using Microsoft.Extensions.DependencyInjection;
using MobileApplication.Services;

namespace MobileApplication.Droid.Notifications
{
	/// <summary>
	/// Handles a tap on the check-in notification's "I'm okay" or "Emergency" action by calling the API, without opening the app, and
	/// replaces the notification with the outcome.
	/// </summary>
	[BroadcastReceiver(Exported = false)]
	public sealed class CheckInActionReceiver : BroadcastReceiver
	{
		/// <summary>Android expects an asynchronous broadcast to finish within about ten seconds; HttpClient's own timeout is 100.</summary>
		private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(8);

		public override void OnReceive(Context? context, Intent? intent)
		{
			var action = intent?.Action;
			var checkInId = intent?.GetLongExtra(CheckInNotifications.ExtraCheckInId, -1) ?? -1;
			if (context is null || action is not (CheckInNotifications.ActionOkay or CheckInNotifications.ActionEmergency))
				return;

			var appContext = context.ApplicationContext ?? context;

			// Replaces the actions before the call, so a second tap cannot send it again.
			CheckInNotifications.ShowNotice(appContext, "Sending…");

			// Keeps the process alive for the call; Finish releases it.
			var pending = GoAsync();
			_ = SendAsync(appContext, action, checkInId, pending);
		}

		private static async Task SendAsync(Context context, string action, long checkInId, PendingResult? pending)
		{
			try
			{
				var api = IPlatformApplication.Current?.Services.GetRequiredService<AmigoAlertApiClient>()
					?? throw new InvalidOperationException("The MAUI application has not been created.");

				using var timeout = new CancellationTokenSource(CallTimeout);

				if (action == CheckInNotifications.ActionOkay)
				{
					await api.RespondOkayAsync(checkInId, timeout.Token);
					CheckInNotifications.ShowNotice(context, "Thank you. Your check-in has been recorded.");
				}
				else
				{
					await api.ActivateEmergencyAsync(ApiSettings.CurrentUserId, timeout.Token);
					CheckInNotifications.ShowNotice(context, "Your emergency contact has been alerted. Open AmigoAlert if this was a mistake.");
				}
			}
			catch (ApiException ex)
			{
				// The API's own explanation, as the home page shows it.
				CheckInNotifications.ShowNotice(context, ex.Message);
			}
			catch (Exception ex)
			{
				// Not retried from here: the request may have reached the API, and a repeated emergency would text the contact again. The
				// notice opens the app, whose buttons show the current status.
				Log.Warn("AmigoAlert.Push", $"Notification action {action} failed: {ex.GetType().Name}: {ex.Message}");
				CheckInNotifications.ShowNotice(context,
					"Could not reach AmigoAlert. Open the app to try again. If you are in danger, call 911.");
			}
			finally
			{
				pending?.Finish();
			}
		}
	}
}
