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

		/// <summary>The emergency button: alerts the user's emergency contact.</summary>
		public async Task ActivateEmergencyAsync(Guid userId, CancellationToken ct = default)
		{
			// TODO: Send the device location once the Geolocation permission is requested.
			using HttpResponseMessage response = await httpClient.PostAsJsonAsync($"api/users/{userId}/emergency",
				new ActivateEmergencyDto(null, null), ct);
			await EnsureSuccessAsync(response, ct);
		}

		/// <summary>"Cancel emergency, I am okay": returns the user to okay and tells the contact to stand down.</summary>
		public async Task CancelEmergencyAsync(Guid userId, CancellationToken ct = default)
		{
			using HttpResponseMessage response = await httpClient.PostAsync($"api/users/{userId}/emergency/cancel", null, ct);
			await EnsureSuccessAsync(response, ct);
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
