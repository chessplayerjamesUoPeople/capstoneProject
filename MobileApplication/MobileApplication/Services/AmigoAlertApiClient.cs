using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace MobileApplication.Services
{
	/// <summary>
	/// Typed client for the AmigoAlert API. Every call throws <see cref="ApiException"/> on a non-success status, and
	/// <see cref="HttpRequestException"/> when the API cannot be reached.
	/// </summary>
	public sealed class AmigoAlertApiClient(HttpClient httpClient)
	{
		/// <summary>
		/// How long a fresh fix is waited for. The notification's Emergency action has about eight seconds for the whole call, so what is
		/// left after this is enough for the request.
		/// </summary>
		private static readonly TimeSpan LocationTimeout = TimeSpan.FromSeconds(3);

		/// <summary>The oldest last-known fix still worth sending when no fresh one arrives in time.</summary>
		private static readonly TimeSpan MaxLastKnownAge = TimeSpan.FromMinutes(10);

		/// <summary>Registers a new user. A 409 means the email address is already registered.</summary>
		public async Task<UserDto> CreateUserAsync(CreateUserDto request, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/users", request, ct);
			await EnsureSuccessAsync(response, ct);

			return (await response.Content.ReadFromJsonAsync<UserDto>(ct))!;
		}

		public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.GetAsync($"api/users/{userId}", ct);
			await EnsureSuccessAsync(response, ct);

			return (await response.Content.ReadFromJsonAsync<UserDto>(ct))!;
		}

		public async Task UpdateUserAsync(Guid userId, UpdateUserDto request, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/users/{userId}", request, ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <returns>The user's active emergency contact, or null when they have none.</returns>
		public async Task<EmergencyContactDto?> GetEmergencyContactAsync(Guid userId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.GetAsync($"api/users/{userId}/emergency-contact", ct);
			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}

			await EnsureSuccessAsync(response, ct);

			return await response.Content.ReadFromJsonAsync<EmergencyContactDto>(ct);
		}

		/// <summary>Adds a contact to a user who has none. The API sends the contact a consent request.</summary>
		public async Task AddEmergencyContactAsync(Guid userId, SetEmergencyContactDto request, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PostAsJsonAsync($"api/users/{userId}/emergency-contact", request, ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>
		/// Replaces the user's contact. A new phone number resets consent to pending and sends a consent request; the same number only
		/// renames the contact.
		/// </summary>
		public async Task ReplaceEmergencyContactAsync(Guid userId, SetEmergencyContactDto request, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/users/{userId}/emergency-contact", request, ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <returns>The unanswered check-in of the user's current occurrence, or null when there is none.</returns>
		public async Task<CheckInDto?> GetPendingCheckInAsync(Guid userId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.GetAsync($"api/users/{userId}/checkins", ct);
			await EnsureSuccessAsync(response, ct);

			List<CheckInDto> checkIns = await response.Content.ReadFromJsonAsync<List<CheckInDto>>(ct) ?? [];

			// Newest first: only the latest occurrence can be answered, and the API answers its reminder and first notification together.
			CheckInDto? latest = checkIns.FirstOrDefault();

			return latest is not null && latest.TimeReceived is null ? latest : null;
		}

		public async Task RespondOkayAsync(long checkInId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/checkins/{checkInId}/response",
				new RespondToCheckInDto(CheckInResponseType.Okay), ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>
		/// The emergency button: alerts the user's emergency contact, with the device location when one can be had in time. Shared by the
		/// in-app button and the notification's Emergency action; the alert is sent without a location rather than held back for one.
		/// </summary>
		public async Task ActivateEmergencyAsync(Guid userId, CancellationToken ct = default)
		{
			Location? location = await TryGetLocationAsync(ct);

			using HttpResponseMessage response = await httpClient.PostAsJsonAsync($"api/users/{userId}/emergency",
				new ActivateEmergencyDto(location?.Latitude, location?.Longitude), ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>
		/// Sends the device location for a check-in this device has just been notified of, so that if the user does not answer, the alert to
		/// their emergency contact can say where they last were. Does nothing when there is no location to send.
		/// </summary>
		public async Task ReportCheckInLocationAsync(long checkInId, CancellationToken ct = default)
		{
			Location? location = await TryGetLocationAsync(ct);
			if (location is null)
			{
				return;
			}

			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/checkins/{checkInId}/location",
				new ActivateEmergencyDto(location.Latitude, location.Longitude), ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>Reports when a check-in notification arrived on this device, by this device's clock.</summary>
		public async Task ReportCheckInDeliveryAsync(long checkInId, DateTimeOffset deliveredAt, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/checkins/{checkInId}/delivery",
				new ReportDeliveryDto(deliveredAt), ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>Registers this device's push channel, so the user's check-in notifications reach it.</summary>
		public async Task RegisterDeviceAsync(Guid userId, Guid installationId, RegisterDeviceDto request, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/users/{userId}/devices/{installationId}", request, ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>"Cancel emergency, I am okay": returns the user to okay and tells the contact to stand down.</summary>
		public async Task CancelEmergencyAsync(Guid userId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PostAsync($"api/users/{userId}/emergency/cancel", null, ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>
		/// The device location, or null when there is none to be had: permission not granted, location switched off, or no fix in time.
		/// Never throws and never prompts, because it also runs from a notification, where there is no screen to prompt on.
		/// </summary>
		private static async Task<Location?> TryGetLocationAsync(CancellationToken ct)
		{
			try
			{
				if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
				{
					return null;
				}

				try
				{
					using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
					timeout.CancelAfter(LocationTimeout);

					Location? fresh = await Geolocation.Default.GetLocationAsync(
						new GeolocationRequest(GeolocationAccuracy.Medium, LocationTimeout), timeout.Token);
					if (fresh is not null)
					{
						return fresh;
					}
				}
				catch (Exception)
				{
					// No fresh fix in time; the last known one may still do.
				}

				Location? lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();

				return lastKnown is not null && DateTimeOffset.UtcNow - lastKnown.Timestamp <= MaxLastKnownAge ? lastKnown : null;
			}
			catch (Exception)
			{
				// A denied background lookup, a disabled provider or an unsupported device: no location is not an error here.
				return null;
			}
		}

		private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
		{
			if (response.IsSuccessStatusCode)
			{
				return;
			}

			string? detail = null;
			try
			{
				using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
				if (problem.RootElement.TryGetProperty("detail", out JsonElement value))
				{
					detail = value.GetString();
				}
			}
			catch (JsonException)
			{
				// Not a ProblemDetails body; fall back to the generic message.
			}

			throw new ApiException(response.StatusCode,
				detail ?? $"The server returned an error ({(int)response.StatusCode}). Please try again.");
		}
	}
}
