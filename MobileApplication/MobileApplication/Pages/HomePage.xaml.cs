using System.Globalization;
using System.Net;
using AmigoAlert.Shared.DTOs;
using MobileApplication.Services;

namespace MobileApplication.Pages;

public partial class HomePage : ContentPage
{
	private readonly AmigoAlertApiClient api;
	private readonly IDispatcherTimer countdownTimer;
	private readonly IDispatcherTimer refreshTimer;

	private HomeStatus? status;
	private CheckInDto? pendingCheckIn;
	private bool isLoading;

	public HomePage(AmigoAlertApiClient api)
	{
		InitializeComponent();

		this.api = api;

		countdownTimer = Dispatcher.CreateTimer();
		countdownTimer.Interval = TimeSpan.FromSeconds(1);
		countdownTimer.Tick += (_, _) => UpdateCountdown();

		// Check-in notifications are not pushed yet, so poll to pick up a pending check-in or a status changed elsewhere.
		refreshTimer = Dispatcher.CreateTimer();
		refreshTimer.Interval = TimeSpan.FromMinutes(1);
		refreshTimer.Tick += async (_, _) => await LoadAsync();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		countdownTimer.Start();
		refreshTimer.Start();
		await LoadAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		countdownTimer.Stop();
		refreshTimer.Stop();
	}

	private async Task LoadAsync()
	{
		if (isLoading)
			return;

		isLoading = true;

		// Only the first load shows the spinner; later refreshes update the screen in place.
		LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = status is null;

		try
		{
			var userTask = api.GetUserAsync(ApiSettings.CurrentUserId);
			var contactTask = api.GetEmergencyContactAsync(ApiSettings.CurrentUserId);
			await Task.WhenAll(userTask, contactTask);

			var previousState = status?.State;
			status = HomeStatus.Create(userTask.Result, contactTask.Result, DateTimeOffset.Now);
			pendingCheckIn = status.State == HomeState.CheckInPending
				? await api.GetPendingCheckInAsync(ApiSettings.CurrentUserId)
				: null;

			ErrorLabel.IsVisible = RetryButton.IsVisible = false;

			// US-08: without a consenting contact the app cannot be used, so send the user to enter a new one.
			if (status.State is HomeState.NoContact or HomeState.ConsentRejected)
			{
				var reason = status.State == HomeState.ConsentRejected ? "consentRejected" : "noContact";
				await Shell.Current.GoToAsync($"{nameof(RegistrationPage)}?mode=update&reason={reason}");
				return;
			}

			Render();

			if (previousState != status.State)
				SemanticScreenReader.Announce(StatusLabel.IsVisible ? StatusLabel.Text : NextCheckInLabel.Text);
		}
		catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
		{
			ShowError(ex is ApiException ? ex.Message : "Could not reach AmigoAlert. Check your internet connection.");
		}
		finally
		{
			LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = false;
			isLoading = false;
		}
	}

	private void Render()
	{
		if (status is null)
			return;

		var state = status.State;
		var contactName = status.ContactName ?? "Your emergency contact";

		StatusLabel.Text = state switch
		{
			HomeState.AwaitingConsent => $"Awaiting consent of emergency contact {contactName}.",
			HomeState.CheckInPending => "Awaiting check-in, please indicate your status",
			HomeState.Escalated => $"Previous check-in failed! {contactName} has been contacted. Click below to cancel the escalation.",
			HomeState.Emergency => $"You have pressed the emergency button! {contactName} has been contacted. Click below if the emergency button was pressed in error.",
			_ => ""
		};
		StatusLabel.IsVisible = StatusLabel.Text.Length > 0;

		NextCheckInLabel.Text = status.NextCheckIn is DateTimeOffset next
			? $"Next check-in at {next.ToString("h:mm tt", CultureInfo.CurrentCulture)}"
			: "No check-ins scheduled";
		NextCheckInLabel.IsVisible = state == HomeState.Okay;
		TimeRemainingLabel.IsVisible = state == HomeState.Okay && status.NextCheckIn is not null;
		UpdateCountdown();

		var inEmergency = state is HomeState.Escalated or HomeState.Emergency;

		// US-23: information can be updated at any time except during an emergency.
		SettingsButton.IsVisible = !inEmergency;
		CheckInButton.IsVisible = state == HomeState.CheckInPending;
		CancelEmergencyButton.IsVisible = inEmergency;

		// US-07: the emergency button is shown but unusable until the contact consents; US-13/14: hidden during an emergency.
		EmergencyButton.IsVisible = !inEmergency;
		EmergencyButton.IsEnabled = state != HomeState.AwaitingConsent;
		EmergencyButton.Source = EmergencyButton.IsEnabled ? "emergency_button.png" : "emergency_button_disabled.png";
		SemanticProperties.SetHint(EmergencyButton, EmergencyButton.IsEnabled
			? "Tap to alert your emergency contact"
			: "Unavailable until your emergency contact accepts");
	}

	private void UpdateCountdown()
	{
		if (status is not { State: HomeState.Okay, NextCheckIn: DateTimeOffset next })
			return;

		var remaining = next - DateTimeOffset.Now;
		if (remaining <= TimeSpan.Zero)
		{
			_ = LoadAsync();
			return;
		}

		TimeRemainingLabel.Text = $"Time remaining: {(int)remaining.TotalHours}:{remaining.Minutes:00}";
	}

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
		RetryButton.IsVisible = true;
		SemanticScreenReader.Announce(message);
	}

	private async void OnSettingsClicked(object? sender, EventArgs e)
	{
		await Shell.Current.GoToAsync($"{nameof(RegistrationPage)}?mode=update");
	}

	private async void OnRetryClicked(object? sender, EventArgs e)
	{
		await LoadAsync();
	}

	private async void OnCheckInClicked(object? sender, EventArgs e)
	{
		if (pendingCheckIn is null)
		{
			await LoadAsync();
			return;
		}

		await SendAsync(CheckInButton, () => api.RespondOkayAsync(pendingCheckIn.Id), "Check-in", conflictIsStale: true);
	}

	public async void OnEmergencyClicked(object? sender, EventArgs e)
	{
		await SendAsync(EmergencyButton, () => api.ActivateEmergencyAsync(ApiSettings.CurrentUserId), "Emergency", conflictIsStale: false);
	}

	private async void OnCancelEmergencyClicked(object? sender, EventArgs e)
	{
		await SendAsync(CancelEmergencyButton, () => api.CancelEmergencyAsync(ApiSettings.CurrentUserId), "Cancel emergency",
			conflictIsStale: true);
	}

	/// <summary>
	/// Runs one button's API call with the button disabled against double taps, reports a failure, and reloads the status either way.
	/// </summary>
	/// <param name="conflictIsStale">True when a 409 only means the screen was out of date (already answered, superseded or already
	/// cancelled), so the reload is the whole answer; false when a 409 must be shown to the user.</param>
	private async Task SendAsync(View button, Func<Task> call, string title, bool conflictIsStale)
	{
		button.IsEnabled = false;
		try
		{
			await call();
		}
		catch (ApiException ex) when (conflictIsStale && ex.StatusCode == HttpStatusCode.Conflict)
		{
		}
		catch (ApiException ex)
		{
			await DisplayAlertAsync(title, ex.Message, "OK");
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			await DisplayAlertAsync(title, "Could not reach AmigoAlert. Check your internet connection and try again.", "OK");
		}
		finally
		{
			button.IsEnabled = true;
		}

		await LoadAsync();
	}
}
