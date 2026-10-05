using System.Globalization;
using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace MobileApplication.Droid.Notifications
{
	/// <summary>
	/// Posts the check-in notification, with its "I'm okay" and "Emergency" actions, and the notices that replace it once an action is
	/// tapped. They share one id, so the reminder replaces the first notification and each notice replaces the one tapped.
	/// </summary>
	internal static class CheckInNotifications
	{
		public const string ActionOkay = "com.amigoalert.app.action.CHECK_IN_OKAY";
		public const string ActionEmergency = "com.amigoalert.app.action.EMERGENCY";
		public const string ExtraCheckInId = "checkInId";

		private const string ChannelId = "checkins";
		private const int NotificationId = 1;

		/// <summary>Shows the check-in described by a push's data.</summary>
		/// <returns>False when the data is not a well-formed check-in, so nothing was shown.</returns>
		public static bool ShowCheckIn(Context context, IDictionary<string, string> data)
		{
			// The keys written by NotificationHubPushService in AmigoAlert.Services.
			if (!data.TryGetValue("checkInId", out var id) || !long.TryParse(id, CultureInfo.InvariantCulture, out var checkInId))
				return false;

			var isReminder = data.TryGetValue("isReminder", out var reminder) && reminder == "true";

			var builder = isReminder
				? NewBuilder(context, "Reminder: please check in", "If you do not answer soon, your emergency contact will be alerted.")
				: NewBuilder(context, "Time to check in", "Are you okay? Tap I'm okay to let AmigoAlert know.");
			builder.SetCategory(NotificationCompat.CategoryReminder);
			// Distinct request codes, or Android would treat the two intents as one and both buttons would do the same thing.
			builder.AddAction(0, "I'm okay", ActionIntent(context, ActionOkay, 1, checkInId));
			builder.AddAction(0, "Emergency", ActionIntent(context, ActionEmergency, 2, checkInId));

			// Removed once the occurrence would have been escalated, when answering it no longer helps.
			if (data.TryGetValue("expiresAt", out var expires) && long.TryParse(expires, CultureInfo.InvariantCulture, out var expiresAt))
			{
				var remaining = expiresAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
				if (remaining > 0)
					builder.SetTimeoutAfter(remaining);
			}

			Post(context, builder);
			return true;
		}

		/// <summary>Replaces the check-in with a notice, without actions, so it cannot be tapped again.</summary>
		public static void ShowNotice(Context context, string text)
		{
			Post(context, NewBuilder(context, "AmigoAlert", text));
		}

		private static NotificationCompat.Builder NewBuilder(Context context, string title, string text)
		{
			// Not chained: the binding types each setter's return as nullable, though every one returns the builder itself.
			var builder = new NotificationCompat.Builder(context, ChannelId);
			builder.SetSmallIcon(Resource.Drawable.ic_stat_amigoalert);
			builder.SetContentTitle(title);
			builder.SetContentText(text);
			builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(text));
			// Heads-up on Android 7.1 and earlier; later versions take it from the channel's importance.
			builder.SetPriority(NotificationCompat.PriorityHigh);
			// The text says nothing private, and the user should not have to unlock the phone to answer.
			builder.SetVisibility(NotificationCompat.VisibilityPublic);
			builder.SetContentIntent(OpenAppIntent(context));
			builder.SetAutoCancel(true);

			return builder;
		}

		private static void Post(Context context, NotificationCompat.Builder builder)
		{
			// Before every post, since a push can start the process without MainActivity ever running. Creating an existing channel only
			// updates its name; the user's settings for it are kept. High importance is what shows it at the top of the screen.
			if (OperatingSystem.IsAndroidVersionAtLeast(26))
			{
				var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
				manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Check-ins", NotificationImportance.High));
			}

			// Silently dropped while the notification permission is denied.
			NotificationManagerCompat.From(context)?.Notify(NotificationId, builder.Build());
		}

		private static PendingIntent OpenAppIntent(Context context)
		{
			var intent = new Intent(context, typeof(MainActivity)).SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);

			return PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
		}

		private static PendingIntent ActionIntent(Context context, string action, int requestCode, long checkInId)
		{
			// Explicit, so only this app's receiver is the target, and immutable, so no other app can rewrite the extras.
			var intent = new Intent(context, typeof(CheckInActionReceiver)).SetAction(action).PutExtra(ExtraCheckInId, checkInId);

			return PendingIntent.GetBroadcast(context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
		}
	}
}
