using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Twilio Programmable SMS settings, bound from the <c>Twilio</c> configuration section. <see cref="AuthToken"/> is a secret and
	/// belongs in Key Vault or user secrets, never in appsettings.json.
	/// </summary>
	public sealed class TwilioOptions
	{
		/// <summary>The configuration section these options bind from.</summary>
		public const string SectionName = "Twilio";

		/// <summary>The Twilio account SID.</summary>
		public string AccountSid { get; set; } = string.Empty;

		/// <summary>The Twilio auth token, used both to send and to validate the <c>X-Twilio-Signature</c> header on callbacks.</summary>
		public string AuthToken { get; set; } = string.Empty;

		/// <summary>
		/// The Messaging Service messages are sent through. Ecuador has no domestic long codes and carriers overwrite the sender ID, so the
		/// Messaging Service chooses the sender rather than a fixed From number.
		/// </summary>
		public string MessagingServiceSid { get; set; } = string.Empty;

		/// <summary>
		/// The public URL of <c>POST /api/sms/status</c>, exactly as Twilio will call it. Signature validation uses this value rather than
		/// the incoming request URL, which differs behind the App Service front end. When empty, no delivery callbacks are requested.
		/// </summary>
		public string StatusCallbackUrl { get; set; } = string.Empty;

		/// <summary>
		/// When true, <see cref="MockSmsService"/> is used instead of Twilio: messages are logged and recorded but never sent, and the other
		/// settings here are ignored. For development and testing only.
		/// </summary>
		public bool UseMockSms { get; set; }
	}
}
