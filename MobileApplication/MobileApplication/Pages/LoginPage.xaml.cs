using System.Net;
using System.Net.Mail;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using MobileApplication.Services;

namespace MobileApplication.Pages;

/// <summary>
/// Log in, step one: the user enters the email address they registered with and a log in code is emailed to it. Opened with
/// <c>?email=…</c> the address is filled in, for example when registration found it already registered.
/// </summary>
public partial class LoginPage : ContentPage, IQueryAttributable
{
	public const string EmailKey = "email";

	private readonly AmigoAlertApiClient api;

	public LoginPage(AmigoAlertApiClient api)
	{
		InitializeComponent();

		this.api = api;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(EmailKey, out var email) && email is string address)
			EmailEntry.Text = address;
	}

	private async void OnSendCodeClicked(object? sender, EventArgs e)
	{
		var email = EmailEntry.Text?.Trim() ?? "";
		if (email.Length == 0)
		{
			ShowError("Email Address is required.");
			return;
		}

		if (!MailAddress.TryCreate(email, out _))
		{
			ShowError("Email Address is not valid.");
			return;
		}

		ErrorLabel.IsVisible = false;
		SendCodeButton.IsEnabled = false;
		SendingIndicator.IsVisible = SendingIndicator.IsRunning = true;
		try
		{
			await api.RequestVerificationCodeAsync(new RequestVerificationCodeDto(email, VerificationPurposeType.Login));
		}
		catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			if (await DisplayAlertAsync("No account found", ex.Message, "Register", "Cancel"))
				await Shell.Current.GoToAsync($"../{nameof(RegistrationPage)}");

			return;
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			ShowError(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.");
			return;
		}
		finally
		{
			SendCodeButton.IsEnabled = true;
			SendingIndicator.IsVisible = SendingIndicator.IsRunning = false;
		}

		await Shell.Current.GoToAsync(nameof(VerifyEmailPage), new Dictionary<string, object>
		{
			[VerifyEmailPage.ModeKey] = VerifyEmailPage.LoginMode,
			[VerifyEmailPage.EmailKey] = email
		});
	}

	private void ShowError(string message)
	{
		// Errors are written out as text, not shown by color alone (NF-06).
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
		SemanticScreenReader.Announce(message);
	}
}
