using Android.App;
using Android.Runtime;
using MobileApplication.Services;

namespace MobileApplication.Droid
{
	[Application]
	public class MainApplication : MauiApplication
	{
		public MainApplication(IntPtr handle, JniHandleOwnership ownership)
			: base(handle, ownership)
		{
		}

		public override void OnCreate()
		{
			base.OnCreate();

			// Once per process: registering or logging in subscribes this device to the user's check-ins at once, not on the next launch.
			ApiSettings.UserIdChanged += (_, _) => _ = Notifications.AmigoAlertFirebaseMessagingService.RegisterAsync();
		}

		protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
	}
}
