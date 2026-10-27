using MobileApplication.Pages;

namespace MobileApplication
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();

			Routing.RegisterRoute(nameof(RegistrationPage), typeof(RegistrationPage));
			Routing.RegisterRoute(nameof(TermsAndConditionsPage), typeof(TermsAndConditionsPage));
			Routing.RegisterRoute(nameof(HelpPage), typeof(HelpPage));
			Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
			Routing.RegisterRoute(nameof(VerifyEmailPage), typeof(VerifyEmailPage));
		}
	}
}
