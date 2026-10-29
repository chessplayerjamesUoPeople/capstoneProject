using System.Net.Mail;
using AmigoAlert.Shared;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using MobileApplication.Services;

namespace MobileApplication.Pages
{
	/// <summary>
	/// The registration form. Opened with <c>?mode=update</c> it becomes the Update Information screen (US-23): the form is filled from
	/// the API and saving updates the user and, if it changed, the emergency contact.
	/// </summary>
	public partial class RegistrationPage : ContentPage, IQueryAttributable
	{
		private readonly AmigoAlertApiClient api;

		private bool isUpdateMode;
		private string? reason;
		private bool isLoaded;
		private EmergencyContactDto? originalContact;

		public RegistrationPage(AmigoAlertApiClient api)
		{
			InitializeComponent();

			this.api = api;
		}

		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			isUpdateMode = query.TryGetValue("mode", out var mode) && Equals(mode, "update");
			reason = query.TryGetValue("reason", out var value) ? value as string : null;
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();

			if (!isUpdateMode || isLoaded)
				return;

			Title = HeaderLabel.Text = "Update Information";
			Shell.SetNavBarIsVisible(this, true);

			// The profile update cannot change the email address.
			EmailEntry.IsReadOnly = true;
			EmailHintLabel.IsVisible = true;

			await LoadAsync();
		}

		private async Task LoadAsync()
		{
			SaveButton.IsEnabled = false;
			LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
			ErrorLabel.IsVisible = false;

			try
			{
				if (await ApiSettings.GetUserIdAsync() is not { } userId)
				{
					ShowErrors("You are not logged in. Please restart the app to log in or register.");
					return;
				}

				var userTask = api.GetUserAsync(userId);
				var contactTask = api.GetEmergencyContactAsync(userId);
				await Task.WhenAll(userTask, contactTask);

				var user = userTask.Result;
				originalContact = contactTask.Result;

				string userName = user.Name;
				string userFirstName = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
				string userLastName = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "";

				string? contactName = originalContact?.Name;
				string contactFirstName = contactName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
				string contactLastName = contactName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "";

				UserFirstNameEntry.Text = userFirstName;
				UserLastNameEntry.Text = userLastName;
				EmailEntry.Text = user.Email;
				PhoneEntry.Text = user.Phone;
				AddressEntry.Text = user.Address;
				LandmarksEditor.Text = user.Landmarks;
				ContactFirstNameEntry.Text = contactFirstName;
				ContactLastNameEntry.Text = contactLastName;
				ContactPhoneEntry.Text = originalContact?.Phone;

				// Show the check-in/emergency status, as on the home page.
				var contactLabel = originalContact?.Name ?? "your emergency contact";
				StatusLabel.Text = HomeStatus.Create(user, originalContact, DateTimeOffset.Now).State switch
				{
					HomeState.Okay => "Status: OK. No check-in or emergency is pending.",
					HomeState.CheckInPending => "Status: awaiting your check-in.",
					HomeState.AwaitingConsent => $"Status: awaiting consent of {contactLabel}.",
					HomeState.Escalated => $"Status: a missed check-in was escalated to {contactLabel}.",
					HomeState.Emergency => $"Status: emergency button pressed; {contactLabel} has been contacted.",
					_ => ""
				};
				StatusLabel.IsVisible = StatusLabel.Text.Length > 0;

				// US-08: explain why the user was sent here.
				NoticeLabel.Text = reason switch
				{
					"consentRejected" => $"{originalContact?.Name ?? "Your emergency contact"} " +
						(originalContact?.ConsentStatus == ConsentStatusType.Revoked
							? "withdrew their consent to be your emergency contact."
							: "declined to be your emergency contact.") + " Please enter a new contact.",
					"noContact" => "Please add an emergency contact to start using AmigoAlert.",
					_ => ""
				};
				NoticeLabel.IsVisible = NoticeLabel.Text.Length > 0;
				if (NoticeLabel.IsVisible) SemanticScreenReader.Announce(NoticeLabel.Text);

				isLoaded = true;
				SaveButton.IsEnabled = true;
			}
			catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
			{
				ShowErrors(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and reopen this page.");
			}
			finally
			{
				LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
			}
		}

		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(UserFirstNameEntry.Text))
				errors.Add("Your First Name is required.");
			if (string.IsNullOrWhiteSpace(UserLastNameEntry.Text))
				errors.Add("Your Last Name is required.");

			if (string.IsNullOrWhiteSpace(EmailEntry.Text))
				errors.Add("Email Address is required.");
			else if (!MailAddress.TryCreate(EmailEntry.Text.Trim(), out _))
				errors.Add("Email Address is not valid.");

			// Phone numbers can be typed as 09XXXXXXXX or +5939XXXXXXXX; both are converted to E.164.
			var phone = "";
			if (!string.IsNullOrWhiteSpace(PhoneEntry.Text) && !PhoneNumberFormat.TryNormalize(PhoneEntry.Text, out phone))
				errors.Add("Mobile Phone Number must look like 0991234567 or +593991234567.");

			if (string.IsNullOrWhiteSpace(AddressEntry.Text))
				errors.Add("Home Address is required.");

			if (string.IsNullOrWhiteSpace(ContactFirstNameEntry.Text))
				errors.Add("Emergency contact First Name is required.");
			if (string.IsNullOrWhiteSpace(ContactLastNameEntry.Text))
				errors.Add("Emergency contact Last Name is required.");

			// An unchanged contact number is kept as stored, even if it is not Ecuadorian and would fail the format check.
			var contactPhone = "";
			var contactPhoneUnchanged = originalContact is not null && ContactPhoneEntry.Text?.Trim() == originalContact.Phone;
			if (contactPhoneUnchanged)
				contactPhone = originalContact!.Phone;
			else if (string.IsNullOrWhiteSpace(ContactPhoneEntry.Text))
				errors.Add("Emergency contact Mobile Phone Number is required.");
			else if (!PhoneNumberFormat.TryNormalize(ContactPhoneEntry.Text, out contactPhone))
				errors.Add("Emergency contact Mobile Phone Number must look like 0991234567 or +593991234567.");

			// US-09: a contact who declined or withdrew consent cannot be added again.
			if (originalContact is { ConsentStatus: ConsentStatusType.Rejected } && contactPhone == originalContact.Phone)
				errors.Add($"{originalContact.Name} declined to be your emergency contact. Please enter a different contact.");
			else if (originalContact is { ConsentStatus: ConsentStatusType.Revoked } && contactPhone == originalContact.Phone)
				errors.Add($"{originalContact.Name} withdrew their consent to be your emergency contact. Please enter a different contact.");

			if (errors.Count > 0)
			{
				ShowErrors("Please fix the following:\n• " + string.Join("\n• ", errors));
				return;
			}

			ErrorLabel.IsVisible = false;

			if (!isUpdateMode)
			{
				// The user is created once the terms are accepted; the phone numbers passed on are already in E.164.
				await Shell.Current.GoToAsync(nameof(TermsAndConditionsPage), new Dictionary<string, object>
				{
					[TermsAndConditionsPage.NameKey] = UserFirstNameEntry.Text.Trim().Replace(" ", string.Empty) + " "
						+ UserLastNameEntry.Text.Trim().Replace(" ", string.Empty),
					[TermsAndConditionsPage.EmailKey] = EmailEntry.Text.Trim(),
					[TermsAndConditionsPage.PhoneKey] = phone,
					[TermsAndConditionsPage.AddressKey] = AddressEntry.Text.Trim(),
					[TermsAndConditionsPage.LandmarksKey] = LandmarksEditor.Text?.Trim() ?? "",
					[TermsAndConditionsPage.ContactNameKey] = ContactFirstNameEntry.Text.Trim().Replace(" ", string.Empty) + " "
						+ ContactLastNameEntry.Text.Trim().Replace(" ", string.Empty),
					[TermsAndConditionsPage.ContactPhoneKey] = contactPhone
				});
				return;
			}

			await SaveUpdateAsync(string.IsNullOrEmpty(phone) ? null : phone, contactPhone);
		}

		private async Task SaveUpdateAsync(string? phone, string contactPhone)
		{
			var contactName = ContactFirstNameEntry.Text.Trim().Replace(" ", string.Empty) + " "
				+ ContactLastNameEntry.Text.Trim().Replace(" ", string.Empty);
			var contactPhoneChanged = originalContact is null || contactPhone != originalContact.Phone;
			var contactChanged = contactPhoneChanged || contactName != originalContact!.Name;

			SaveButton.IsEnabled = false;
			try
			{
				if (await ApiSettings.GetUserIdAsync() is not { } userId)
				{
					ShowErrors("You are not logged in. Please restart the app to log in or register.");
					return;
				}

				await api.UpdateUserAsync(userId, new UpdateUserDto(UserFirstNameEntry.Text.Trim().Replace(" ", string.Empty) + " "
						+ UserLastNameEntry.Text.Trim().Replace(" ", string.Empty),
					phone, AddressEntry.Text.Trim(), string.IsNullOrWhiteSpace(LandmarksEditor.Text)
					? null : LandmarksEditor.Text.Trim()));

				if (contactChanged)
				{
					var contact = new SetEmergencyContactDto(contactName, contactPhone);
					if (originalContact is null)
						await api.AddEmergencyContactAsync(userId, contact);
					else
						await api.ReplaceEmergencyContactAsync(userId, contact);
				}

				// US-23: a new number means the account waits for the new contact's consent.
				if (contactPhoneChanged)
					await DisplayAlertAsync("Consent requested",
						$"A consent request was sent to {contactName}. You can use AmigoAlert once they accept.", "OK");

				await Shell.Current.GoToAsync("..");
			}
			catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
			{
				ShowErrors(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection and try again.");
			}
			finally
			{
				SaveButton.IsEnabled = true;
			}
		}

		private void ShowErrors(string message)
		{
			// Errors are written out as text, not shown by color alone (NF-06).
			ErrorLabel.Text = message;
			ErrorLabel.IsVisible = true;
			SemanticScreenReader.Announce(ErrorLabel.Text);
		}
	}
}
