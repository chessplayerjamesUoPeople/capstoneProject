using System.Net;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using MobileApplication.Services;

namespace MobileApplication.Pages;

/// <summary>
/// The second step of registration. The details from the registration form arrive as query attributes; accepting the terms emails a
/// registration code and passes the details on to <see cref="VerifyEmailPage"/>, which creates the user once the code is entered.
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

	private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		if (!AcceptTermsAndConditionsCheckBox.IsChecked)
		{
			ShowError("Please accept the Terms and Conditions to continue.");
			return;
		}

		ErrorLabel.IsVisible = false;
		SaveButton.IsEnabled = false;
		try
		{
			await api.RequestVerificationCodeAsync(new RequestVerificationCodeDto(Detail(EmailKey), VerificationPurposeType.Registration));
		}
		catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
		{
			// Registered before, for example before the app's data was cleared: logging in recovers the account.
			if (await DisplayAlertAsync("Already registered", ex.Message, "Log in", "Cancel"))
				await Shell.Current.GoToAsync($"../../{nameof(LoginPage)}",
					new Dictionary<string, object> { [LoginPage.EmailKey] = Detail(EmailKey) });

			return;
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			ShowError(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.");
			return;
		}
		finally
		{
			SaveButton.IsEnabled = true;
		}

		// The user is created once the emailed code is entered, with the details passed on unchanged.
		var verifyDetails = new Dictionary<string, object>(details) { [VerifyEmailPage.ModeKey] = VerifyEmailPage.RegisterMode };
		await Shell.Current.GoToAsync(nameof(VerifyEmailPage), verifyDetails);
	}

	private void ShowError(string message)
	{
		// Errors are written out as text, not shown by color alone (NF-06).
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
		SemanticScreenReader.Announce(message);
	}
}
