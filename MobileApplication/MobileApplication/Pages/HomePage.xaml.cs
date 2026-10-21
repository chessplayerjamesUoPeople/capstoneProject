using System.Globalization;
using System.Net;
using AmigoAlert.Shared.DTOs;
using Microsoft.Extensions.Logging;
using MobileApplication.Services;

namespace MobileApplication.Pages;

public partial class HomePage : ContentPage
{
	/// <summary>Set once the location permission has been offered, so the prompt appears once per launch rather than on every visit.</summary>
	private static bool locationPermissionOffered;

	/// <summary>Failed refreshes in a row before the error replaces a screen that has already loaded; one is usually a passing blip.</summary>
	private const int RefreshFailuresBeforeError = 2;

	private readonly AmigoAlertApiClient api;
	private readonly ILogger<HomePage> logger;
	private readonly IDispatcherTimer countdownTimer;
	private readonly IDispatcherTimer refreshTimer;
	private readonly IDispatcherTimer emergencyTimer;

	private HomeStatus? status;
	private CheckInDto? pendingCheckIn;
	private bool isLoading;
	private int consecutiveRefreshFailures;

	/// <summary>The window whose background and foreground events this page is subscribed to while it is shown.</summary>
	private Window? subscribedWindow;

	public HomePage(AmigoAlertApiClient api, ILogger<HomePage> logger)
	{
		InitializeComponent();

		this.api = api;
		this.logger = logger;

		countdownTimer = Dispatcher.CreateTimer();
		countdownTimer.Interval = TimeSpan.FromSeconds(1);
		countdownTimer.Tick += (_, _) => UpdateCountdown();

		// Poll to pick up a pending check-in or a status changed elsewhere while the page is open.
		refreshTimer = Dispatcher.CreateTimer();
		refreshTimer.Interval = TimeSpan.FromMinutes(1);
		refreshTimer.Tick += async (_, _) => await LoadAsync(isRefresh: true);

		emergencyTimer = Dispatcher.CreateTimer();
		emergencyTimer.Interval = TimeSpan.FromSeconds(30);
		emergencyTimer.Tick += async (_, _) => await EmergencyTimer_Tick();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// OnAppearing and OnDisappearing only follow navigation, not the app going to the background and back, so follow the window
		// too: polling from the background fails because Android cuts off network access there, and it leaves a false error on screen.
		subscribedWindow = Window;
		if (subscribedWindow is not null)
		{
			subscribedWindow.Stopped += OnWindowStopped;
			subscribedWindow.Resumed += OnWindowResumed;
		}

		StartTimers();
		await LoadAsync();
		await EnsureLocationPermissionAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		if (subscribedWindow is not null)
		{
			subscribedWindow.Stopped -= OnWindowStopped;
			subscribedWindow.Resumed -= OnWindowResumed;
			subscribedWindow = null;
		}

		StopTimers();
	}

	private void OnWindowStopped(object? sender, EventArgs e) => StopTimers();

	private async void OnWindowResumed(object? sender, EventArgs e)
	{
		StartTimers();
		await LoadAsync(isRefresh: true);
	}

	private void StartTimers()
	{
		countdownTimer.Start();
		refreshTimer.Start();
	}

	private void StopTimers()
	{
		countdownTimer.Stop();
		refreshTimer.Stop();
	}

	/// <summary>
	/// Explains why the app wants the device location, then asks for it. Location is optional: without it the alert to the emergency
	/// contact carries the registered address instead. "Allow all the time" is what lets the notification's Emergency action, and the
	/// location sent when a check-in arrives, work while the app is closed.
	/// </summary>
	private async Task EnsureLocationPermissionAsync()
	{
		// Once per launch, and not while the user is being sent to enter an emergency contact.
		if (locationPermissionOffered || status is null or { State: HomeState.NoContact or HomeState.ConsentRejected })
			return;

		locationPermissionOffered = true;

		if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted
			&& await Permissions.CheckStatusAsync<Permissions.LocationAlways>() == PermissionStatus.Granted)
			return;

		// Android needs the two requests separately, and the user needs to know why before the system asks.
		var proceed = await DisplayAlertAsync("Share your location?",
			"AmigoAlert shares your location with your emergency contact only when you press the emergency button or miss a check-in. " +
			"Please allow location access, and choose \"Allow all the time\" so it also works when the app is closed.",
			"Continue", "Not now");
		if (!proceed)
			return;

		if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted)
			await Permissions.RequestAsync<Permissions.LocationAlways>();
	}

	/// <param name="isRefresh">True for a reload of a screen already shown (the timer, a return from the background), where a single
	/// failure keeps the current screen rather than replacing it with an error; false for the first load and the Retry button.</param>
	private async Task LoadAsync(bool isRefresh = false)
	{
		if (isLoading)
			return;

		isLoading = true;

		// Only the first load shows the spinner; later refreshes update the screen in place.
		LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = status is null;

		try
		{
			// First run: nobody has registered on this device yet.
			if (await ApiSettings.GetUserIdAsync() is not { } userId)
			{
				await Shell.Current.GoToAsync(nameof(RegistrationPage));
				return;
			}

			var userTask = api.GetUserAsync(userId);
			var contactTask = api.GetEmergencyContactAsync(userId);
			await Task.WhenAll(userTask, contactTask);

			var previousState = status?.State;
			status = HomeStatus.Create(userTask.Result, contactTask.Result, DateTimeOffset.Now);
			pendingCheckIn = status.State == HomeState.CheckInPending
				? await api.GetPendingCheckInAsync(userId)
				: null;

			consecutiveRefreshFailures = 0;
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
			logger.LogWarning(ex, "Loading the home status failed (refresh: {IsRefresh})", isRefresh);

			if (isRefresh && status is not null && ++consecutiveRefreshFailures < RefreshFailuresBeforeError)
				return;

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
			_ = LoadAsync(isRefresh: true);
			return;
		}

		TimeRemainingLabel.Text = $"Time remaining: {(int)remaining.TotalHours}h {remaining.Minutes:00}m";
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
		refreshTimer.Stop();
		emergencyTimer.Start();
		status = new HomeStatus(HomeState.Emergency, status?.ContactName, status?.NextCheckIn);
		Render();
	}

	private async Task EmergencyTimer_Tick()
	{
		emergencyTimer.Stop();
		refreshTimer.Start();
		await SendAsync(EmergencyButton, async () => await api.ActivateEmergencyAsync(await RequireUserIdAsync()), "Emergency",
			conflictIsStale: false);
	}

	private async void OnCancelEmergencyClicked(object? sender, EventArgs e)
	{
		if (emergencyTimer.IsRunning)
		{
			emergencyTimer.Stop();
			refreshTimer.Start();
			await LoadAsync(true);
		}
		else
		{
			await SendAsync(CancelEmergencyButton, async () => await api.CancelEmergencyAsync(await RequireUserIdAsync()), "Cancel emergency",
				conflictIsStale: true);
		}
	}

	/// <summary>The registered user's ID. The buttons only show for a loaded user, so a missing ID means storage failed since then.</summary>
	private static async Task<Guid> RequireUserIdAsync() =>
		await ApiSettings.GetUserIdAsync() ?? throw new ApiException(HttpStatusCode.Unauthorized, "You are not registered yet. Please restart the app to register.");

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
			logger.LogWarning(ex, "{Action} could not reach the API", title);
			await DisplayAlertAsync(title, "Could not reach AmigoAlert. Check your internet connection and try again.", "OK");
		}
		finally
		{
			button.IsEnabled = true;
		}

		await LoadAsync();
	}
}
