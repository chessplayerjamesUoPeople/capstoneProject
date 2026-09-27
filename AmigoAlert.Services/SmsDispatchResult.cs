using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// The outcome of handing a message to the SMS provider. Acceptance is not delivery: delivery arrives later on the status callback.
	/// </summary>
	/// <param name="Succeeded">True when the provider accepted the message and its <c>Sms</c> row was recorded.</param>
	/// <param name="ProviderMessageId">The provider's identifier for the message (the Twilio SID), or null when it was rejected.</param>
	/// <param name="ErrorCode">The provider error code when the message was rejected, or null.</param>
	public sealed record SmsDispatchResult(bool Succeeded, string? ProviderMessageId, int? ErrorCode);
}
