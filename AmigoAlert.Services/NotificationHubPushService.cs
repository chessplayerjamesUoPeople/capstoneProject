using AmigoAlert.Services.Rules;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AmigoAlert.Services
{
	/// <inheritdoc cref="IPushNotificationService"/>
	/// <remarks>
	/// A singleton, so the process has one <see cref="NotificationHubClient"/> and one HttpClient. The client is created on first use and
	/// every failure, including missing configuration, is logged and returned as false: <see cref="EscalationService"/> depends on this
	/// service, so a push misconfiguration must not stop the API's check-in and emergency endpoints from resolving.
	/// </remarks>
	internal sealed class NotificationHubPushService(IOptions<NotificationHubOptions> options, ILogger<NotificationHubPushService> logger)
		: IPushNotificationService
	{
		private readonly Lazy<NotificationHubClient> hub = new Lazy<NotificationHubClient>(() => CreateClient(options.Value),
			LazyThreadSafetyMode.PublicationOnly);

		/// <inheritdoc cref="IPushNotificationService.RegisterDeviceAsync"/>
		public async Task<bool> RegisterDeviceAsync(Guid userId, Guid installationId, string pushChannel, CancellationToken ct = default)
		{
			Installation installation = new Installation
			{
				InstallationId = installationId.ToString("D"),
				Platform = NotificationPlatform.FcmV1,
				PushChannel = pushChannel,
				// Set here and nowhere else, so a client cannot subscribe itself to another user's check-ins.
				Tags = [UserTag(userId)]
			};

			try
			{
				await hub.Value.CreateOrUpdateInstallationAsync(installation, ct);
				return true;
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				// The push channel identifies the device, so it is left out of the log.
				logger.LogError(exception, "Notification Hubs rejected the registration of installation {InstallationId} for user {UserId}.",
					installationId, userId);
				return false;
			}
		}

		/// <inheritdoc cref="IPushNotificationService.SendCheckInAsync"/>
		public async Task<bool> SendCheckInAsync(Guid userId, long checkInId, bool isReminder, DateTimeOffset sentAt,
			CancellationToken ct = default)
		{
			try
			{
				// Only the concrete client exposes the FCM v1 send; INotificationHubClient does not.
				await hub.Value.SendFcmV1NativeNotificationAsync(CheckInPayload(checkInId, isReminder, sentAt), UserTag(userId), ct);
				return true;
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				logger.LogError(exception, "Notification Hubs rejected the push for check-in {CheckInId} to user {UserId}.", checkInId, userId);
				return false;
			}
		}

		/// <summary>
		/// The FCM v1 message for one check-in. Data-only, so the app draws the notification with its two actions in every app state; a
		/// notification block would be drawn by Android without them. Identifiers only: the wording lives in the app. The data keys are read
		/// by <c>CheckInNotifications</c> in the Android project.
		/// </summary>
		private static string CheckInPayload(long checkInId, bool isReminder, DateTimeOffset sentAt)
		{
			// Worth delivering only until the occurrence would be escalated.
			TimeSpan lifetime = TimeSpan.FromMinutes(isReminder
				? EscalationRules.GraceMinutes
				: EscalationRules.ReminderAfterMinutes + EscalationRules.GraceMinutes);

			var message = new
			{
				message = new
				{
					android = new
					{
						priority = "high",
						ttl = string.Create(CultureInfo.InvariantCulture, $"{(long)lifetime.TotalSeconds}s"),
						// A device that was offline receives only the newest check-in.
						collapse_key = "checkin"
					},
					// FCM accepts only string values here.
					data = new Dictionary<string, string>
					{
						["checkInId"] = checkInId.ToString(CultureInfo.InvariantCulture),
						["isReminder"] = isReminder ? "true" : "false",
						["expiresAt"] = (sentAt + lifetime).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)
					}
				}
			};

			return JsonSerializer.Serialize(message);
		}

		/// <summary>The tag every installation of a user carries. Tags are not encrypted at rest, so it holds only the opaque user id.</summary>
		private static string UserTag(Guid userId) => $"user:{userId:D}";

		private static NotificationHubClient CreateClient(NotificationHubOptions settings)
		{
			if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.HubName))
			{
				throw new InvalidOperationException("NotificationHub:ConnectionString and NotificationHub:HubName must both be configured.");
			}

			// Named explicitly, since it otherwise surfaces as an opaque parse error. The value itself is never logged.
			if (settings.ConnectionString.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("NotificationHub:ConnectionString is an unresolved Key Vault reference. Check the app's " +
					"managed identity has the Key Vault Secrets User role.");
			}

			// Short timeout and one retry: the WebJob acts on users one at a time, so a slow hub must not hold up everyone after them.
			return new NotificationHubClient(settings.ConnectionString, settings.HubName, new NotificationHubSettings
			{
				OperationTimeout = TimeSpan.FromSeconds(10),
				RetryOptions = new NotificationHubRetryOptions { MaxRetries = 1 }
			});
		}
	}
}
