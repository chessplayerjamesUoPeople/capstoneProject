using MobileApplication.Pages;

namespace MobileApplication
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();

			Routing.RegisterRoute(nameof(RegistrationPage), typeof(RegistrationPage));
		}
	}
}
