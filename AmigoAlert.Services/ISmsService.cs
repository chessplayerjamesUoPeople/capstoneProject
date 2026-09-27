using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Sends SMS messages to emergency contacts and validates the provider's delivery callbacks.
	/// </summary>
	/// <remarks>
	/// The one justified service interface: Twilio costs money per message and needs network access and credentials, so tests substitute a
	/// fake to exercise the escalation and consent paths end to end.
	/// </remarks>
	public interface ISmsService
	{
		/// <summary>
		/// Sends a message and, when the provider accepts it, records the <c>Sms</c> row with the provider's SID. A rejected message is not
		/// recorded and does not throw; the result carries the provider's error code instead.
		/// </summary>
		/// <param name="emergencyContactId">The contact the message is sent to; the user is reached through the contact.</param>
		/// <param name="toPhone">The destination number in E.164 format.</param>
		/// <param name="body">The message text; at most <see cref="SmsMessageComposer.MaxLength"/> characters.</param>
		/// <param name="messageType">The kind of message, recorded against the row.</param>
		/// <param name="statusLogId">The status change that caused the message, or null when there is none (consent requests).</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the provider accepted the message, with its SID or error code.</returns>
		/// <exception cref="ArgumentException"><paramref name="body"/> exceeds <see cref="SmsMessageComposer.MaxLength"/>.</exception>
		Task<SmsDispatchResult> SendAsync(Guid emergencyContactId, string toPhone, string body, SmsMessageTypeEnum messageType,
			long? statusLogId, CancellationToken ct = default);

		/// <summary>
		/// Determines whether a delivery status callback was genuinely sent by the provider, by checking its <c>X-Twilio-Signature</c>
		/// header against every posted form field.
		/// </summary>
		/// <param name="parameters">All form fields of the callback, unmodified.</param>
		/// <param name="signature">The value of the <c>X-Twilio-Signature</c> header, or null if absent.</param>
		/// <returns>True if the signature is present and valid; otherwise, false.</returns>
		bool IsValidStatusCallback(IDictionary<string, string> parameters, string? signature);
	}
}
