using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Registers users' devices for push notifications and pushes check-ins to them.
	/// </summary>
	/// <remarks>
	/// Push is best effort: a provider failure is logged and returned as false, never thrown. Escalation does not depend on a push arriving.
	/// </remarks>
	public interface IPushNotificationService
	{
		/// <summary>
		/// Creates or replaces the registration of one device, so that check-ins pushed to <paramref name="userId"/> reach it.
		/// </summary>
		/// <param name="userId">The user the device belongs to.</param>
		/// <param name="installationId">The identifier the app generated for this installation; stable across launches.</param>
		/// <param name="pushChannel">The FCM registration token of the device.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the provider accepted the registration; otherwise, false.</returns>
		Task<bool> RegisterDeviceAsync(Guid userId, Guid installationId, string pushChannel, CancellationToken ct = default);

		/// <summary>
		/// Pushes a check-in to every device registered for the user, shown as a notification with "I'm okay" and "Emergency" actions.
		/// </summary>
		/// <param name="userId">The user to notify.</param>
		/// <param name="checkInId">The check-in the "I'm okay" action answers.</param>
		/// <param name="isReminder">True for the reminder, false for the first notification of the occurrence.</param>
		/// <param name="sentAt">When the check-in was recorded as sent; the push expires relative to it.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the provider accepted the push; otherwise, false.</returns>
		Task<bool> SendCheckInAsync(Guid userId, long checkInId, bool isReminder, DateTimeOffset sentAt, CancellationToken ct = default);
	}
}
