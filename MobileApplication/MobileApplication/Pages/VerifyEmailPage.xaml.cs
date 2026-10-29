using System.Text.RegularExpressions;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using MobileApplication.Services;

namespace MobileApplication.Pages;

/// <summary>
/// The user enters the code emailed to them. In <see cref="LoginMode"/> the code logs them in, which restores their account on this
/// device. In <see cref="RegisterMode"/> the registration details arrive as query attributes, keyed as for
/// <see cref="TermsAndConditionsPage"/>, and the code creates the user, remembers the new user's ID and adds the emergency contact.
/// </summary>
public partial class VerifyEmailPage : ContentPage, IQueryAttributable
{
	public const string ModeKey = "mode";
	public const string EmailKey = TermsAndConditionsPage.EmailKey;
	public const string LoginMode = "login";
	public const string RegisterMode = "register";

	/// <summary>How long after a code is sent before another can be requested; matches the API's resend cooldown.</summary>
	private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

	private readonly AmigoAlertApiClient api;
	private readonly IDispatcherTimer resendTimer;

	private IDictionary<string, object> details = new Dictionary<string, object>();
	private bool isRegistration;
	private DateTimeOffset resendAvailableAt;

	public VerifyEmailPage(AmigoAlertApiClient api)
	{
		InitializeComponent();

		this.api = api;

		resendTimer = Dispatcher.CreateTimer();
		resendTimer.Interval = TimeSpan.FromSeconds(1);
		resendTimer.Tick += (_, _) => UpdateResendButton();
	}

	private string Email => Detail(EmailKey);

	private VerificationPurposeType Purpose => isRegistration ? VerificationPurposeType.Registration : VerificationPurposeType.Login;

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		details = query;
		isRegistration = Equals(Detail(ModeKey), RegisterMode);

		// The previous page has just sent a code.
		resendAvailableAt = DateTimeOffset.Now + ResendCooldown;
		InstructionsLabel.Text = $"We sent a 6-digit code to {Email}. Enter it below to " +
			(isRegistration ? "confirm your email address and finish registering." : "log in.");
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		UpdateResendButton();
		resendTimer.Start();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		resendTimer.Stop();
	}

	private string Detail(string key) => details.TryGetValue(key, out var value) ? value as string ?? "" : "";

	private void UpdateResendButton()
	{
		var remaining = resendAvailableAt - DateTimeOffset.Now;
		ResendButton.IsEnabled = remaining <= TimeSpan.Zero;
		ResendButton.Text = ResendButton.IsEnabled
			? "Send a new code"
			: $"Send a new code ({Math.Ceiling(remaining.TotalSeconds):0})";
	}

	private async void OnResendClicked(object? sender, EventArgs e)
	{
		ErrorLabel.IsVisible = false;
		ResendButton.IsEnabled = false;
		try
		{
			await api.RequestVerificationCodeAsync(new RequestVerificationCodeDto(Email, Purpose));

			resendAvailableAt = DateTimeOffset.Now + ResendCooldown;
			CodeEntry.Text = "";
			await DisplayAlertAsync("New code sent", $"A new code was sent to {Email}. The previous code no longer works.", "OK");
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			ShowError(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.");
		}
		finally
		{
			UpdateResendButton();
		}
	}

	private async void OnVerifyClicked(object? sender, EventArgs e)
	{
		var code = CodeEntry.Text?.Trim() ?? "";
		if (!Regex.IsMatch(code, VerificationCodeFormat.Pattern))
		{
			ShowError($"Enter the {VerificationCodeFormat.Length}-digit code from the email.");
			return;
		}

		ErrorLabel.IsVisible = false;
		VerifyButton.IsEnabled = false;
		BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
		try
		{
			if (isRegistration)
				await RegisterAsync(code);
			else
				await LogInAsync(code);
		}
		finally
		{
			VerifyButton.IsEnabled = true;
			BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
		}
	}

	private async Task LogInAsync(string code)
	{
		try
		{
			var user = await api.LogInAsync(new LogInDto(Email, code));
			await ApiSettings.SetUserIdAsync(user.Id);
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			ShowError(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.");
			return;
		}

		// Home picks up where the account left off, including asking for an emergency contact if it has none.
		await Shell.Current.GoToAsync("//HomePage");
	}

	private async Task RegisterAsync(string code)
	{
		var contactName = Detail(TermsAndConditionsPage.ContactNameKey);
		var registered = false;
		try
		{
			var phone = Detail(TermsAndConditionsPage.PhoneKey);
			var landmarks = Detail(TermsAndConditionsPage.LandmarksKey);
			var user = await api.CreateUserAsync(new CreateUserDto(Detail(TermsAndConditionsPage.NameKey), Email,
				phone.Length > 0 ? phone : null, Detail(TermsAndConditionsPage.AddressKey), landmarks.Length > 0 ? landmarks : null,
				ApiSettings.FirstNotificationTime, ApiSettings.SecondNotificationTime, ApiSettings.TermsVersion, code));

			// Stored straight away: if adding the contact fails, the home page asks for one rather than registering a second account.
			await ApiSettings.SetUserIdAsync(user.Id);
			registered = true;

			await api.AddEmergencyContactAsync(user.Id,
				new SetEmergencyContactDto(contactName, Detail(TermsAndConditionsPage.ContactPhoneKey)));

			await DisplayAlertAsync("Consent requested",
				$"A consent request was sent to {contactName}. You can use AmigoAlert once they accept.", "OK");
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
