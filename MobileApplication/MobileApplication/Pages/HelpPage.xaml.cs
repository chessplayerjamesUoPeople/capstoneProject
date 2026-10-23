namespace MobileApplication.Pages;

/// <summary>Plain-language help for using the app, opened from the help button on the home page.</summary>
public partial class HelpPage : ContentPage
{
	public HelpPage()
	{
		InitializeComponent();
	}

	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
