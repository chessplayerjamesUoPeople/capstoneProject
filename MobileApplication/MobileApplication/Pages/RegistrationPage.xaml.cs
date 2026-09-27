using System.Net.Mail;
using AmigoAlert.Shared;

namespace MobileApplication.Pages
{
	public partial class RegistrationPage : ContentPage
	{
		public RegistrationPage()
		{
			InitializeComponent();
		}

		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(NameEntry.Text))
				errors.Add("Name is required.");

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

			if (string.IsNullOrWhiteSpace(ContactNameEntry.Text))
				errors.Add("Emergency contact Name is required.");

			var contactPhone = "";
			if (string.IsNullOrWhiteSpace(ContactPhoneEntry.Text))
				errors.Add("Emergency contact Mobile Phone Number is required.");
			else if (!PhoneNumberFormat.TryNormalize(ContactPhoneEntry.Text, out contactPhone))
				errors.Add("Emergency contact Mobile Phone Number must look like 0991234567 or +593991234567.");

			if (errors.Count > 0)
			{
				// Errors are written out as text, not shown by color alone (NF-06).
				ErrorLabel.Text = "Please fix the following:\n• " + string.Join("\n• ", errors);
				ErrorLabel.IsVisible = true;
				SemanticScreenReader.Announce(ErrorLabel.Text);
				return;
			}

			ErrorLabel.IsVisible = false;

			// TODO: call the API with phone and contactPhone (already in E.164).
			await DisplayAlertAsync("Registration", "Your details are valid.", "OK");
		}
	}
}
