using AmigoAlert.Shared.DTOs;
using MobileApplication.Services;

namespace MobileApplication.Pages;

/// <summary>
/// The last step of registration. The details from the registration form arrive as query attributes; accepting the terms creates the
/// user, remembers the new user's ID and adds the emergency contact.
/// </summary>
public partial class TermsAndConditionsPage : ContentPage, IQueryAttributable
{
	public const string NameKey = "name";
	public const string EmailKey = "email";
	public const string PhoneKey = "phone";
	public const string AddressKey = "address";
	public const string LandmarksKey = "landmarks";
	public const string ContactNameKey = "contactName";
	public const string ContactPhoneKey = "contactPhone";

	private readonly AmigoAlertApiClient api;

	private IDictionary<string, object> details = new Dictionary<string, object>();

	public TermsAndConditionsPage(AmigoAlertApiClient api)
	{
		InitializeComponent();

		this.api = api;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		details = query;
	}

	private string Detail(string key) => details.TryGetValue(key, out var value) ? value as string ?? "" : "";

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		if (!AcceptTermsAndConditionsCheckBox.IsChecked)
		{
			ShowError("Please accept the Terms and Conditions to continue.");
			return;
		}

		ErrorLabel.IsVisible = false;
		SaveButton.IsEnabled = false;
		var registered = false;
		try
		{
			var phone = Detail(PhoneKey);
			var landmarks = Detail(LandmarksKey);
			var user = await api.CreateUserAsync(new CreateUserDto(Detail(NameKey), Detail(EmailKey), phone.Length > 0 ? phone : null,
				Detail(AddressKey), landmarks.Length > 0 ? landmarks : null, ApiSettings.FirstNotificationTime,
				ApiSettings.SecondNotificationTime, ApiSettings.TermsVersion));

			// Stored straight away: if adding the contact fails, the home page asks for one rather than registering a second account.
			await ApiSettings.SetUserIdAsync(user.Id);
			registered = true;

			await api.AddEmergencyContactAsync(user.Id, new SetEmergencyContactDto(Detail(ContactNameKey), Detail(ContactPhoneKey)));

			await DisplayAlertAsync("Consent requested",
				$"A consent request was sent to {Detail(ContactNameKey)}. You can use AmigoAlert once they accept.", "OK");
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			var message = ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.";

			if (registered)
			{
				// The account exists but the contact was not saved; Home asks for one.
				await DisplayAlertAsync("Emergency contact not saved", message, "OK");
				await Shell.Current.GoToAsync("//HomePage");
			}
			else
			{
				ShowError(message);
				SaveButton.IsEnabled = true;
			}

			return;
		}

		await Shell.Current.GoToAsync("//HomePage");
	}

	private void ShowError(string message)
	{
		// Errors are written out as text, not shown by color alone (NF-06).
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
		SemanticScreenReader.Announce(message);
	}
}
