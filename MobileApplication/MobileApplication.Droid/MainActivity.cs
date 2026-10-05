using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MobileApplication.Droid
{
	[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
	public class MainActivity : MauiAppCompatActivity
	{
		protected override void OnCreate(Bundle? savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			// Android 13+ shows no notifications until the user allows them.
			if (OperatingSystem.IsAndroidVersionAtLeast(33)
				&& CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
			{
				RequestPermissions([Android.Manifest.Permission.PostNotifications], 0);
			}

			// On every launch, so a registration that failed earlier, or one the hub dropped, recovers.
			_ = Notifications.AmigoAlertFirebaseMessagingService.RegisterAsync();
		}
	}
}
