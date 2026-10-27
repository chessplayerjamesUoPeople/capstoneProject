using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// The outcome of handing an email to the provider. Acceptance is not delivery.
	/// </summary>
	/// <param name="Succeeded">True when the provider accepted the message.</param>
	/// <param name="ProviderMessageId">The provider's identifier for the message, or null when it was rejected.</param>
	public sealed record EmailDispatchResult(bool Succeeded, string? ProviderMessageId);
}
