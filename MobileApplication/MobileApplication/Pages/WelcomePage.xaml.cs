namespace MobileApplication.Pages;

/// <summary>
/// The first screen when no user is stored on this device: on first run, after a reinstall, or after the app's data was cleared. A user
/// who registered before logs in to recover their account; a new user registers.
/// </summary>
public partial class WelcomePage : ContentPage
{
	public WelcomePage()
	{
		InitializeComponent();
	}

	private async void OnLogInClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(LoginPage));

	private async void OnRegisterClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(RegistrationPage));
}
