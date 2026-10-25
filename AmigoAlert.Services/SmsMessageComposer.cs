using AmigoAlert.Services.Rules;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Composes the SMS bodies sent to emergency contacts. Pure and deterministic, like <see cref="EscalationRules"/>.
	/// </summary>
	/// <remarks>
	/// Ecuadorian carriers do not support two-way SMS, so every message is self-contained. Movistar limits a message to
	/// <see cref="MaxLength"/> characters, so the variable part of each body (a name or an address) is truncated to whatever room the fixed
	/// text leaves. The fixed text is plain ASCII, which is within the GSM-7 alphabet; any character outside it would force UCS-2, whose
	/// single-segment limit is 70 characters.
	/// </remarks>
	public static class SmsMessageComposer
	{
		/// <summary>The maximum length of one SMS on Movistar Ecuador.</summary>
		public const int MaxLength = 160;

		/// <summary>The longest name placed ahead of an address, so the address keeps enough room to be useful.</summary>
		private const int MaxNameBeforeAddress = 40;

		/// <summary>A first name longer than this switches the consent request to the full name and a separate link message.</summary>
		private const int MaxFirstNameForSingleConsent = 5;

		private const string Prefix = "AmigoAlert: ";

		/// <summary>
		/// The request asking a new contact to agree to receive alerts, linking to the consent page.
		/// </summary>
		/// <param name="userName">The name of the user the contact would receive alerts for.</param>
		/// <param name="consentUrl">The full consent page URL, including the token.</param>
		/// <returns>The message body.</returns>
		public static string ConsentRequest(string userName, string consentUrl)
		{
			return Compose(Prefix, userName, $" added you as their emergency contact: {consentUrl}");
		}

		/// <summary>
		/// The consent request as one or two messages. When the user's first name is longer than <see cref="MaxFirstNameForSingleConsent"/>
		/// characters the full name is used and the link moves to a second message; otherwise one message carries the first name and the link.
		/// </summary>
		/// <param name="userName">The user's full name.</param>
		/// <param name="consentUrl">The full consent page URL, including the token.</param>
		/// <returns>The message bodies, in the order they must be sent.</returns>
		public static IReadOnlyList<string> ConsentRequestMessages(string userName, string consentUrl)
		{
			string fullName = userName.Trim();
			int space = fullName.IndexOf(' ');
			string firstName = space < 0 ? fullName : fullName[..space];

			if (firstName.Length <= MaxFirstNameForSingleConsent)
			{
				return new[] { ConsentRequest(firstName, consentUrl) };
			}

			return new[]
			{
				Compose(Prefix, fullName, " added you as their emergency contact. You will also receive a second message with the link to the consent form."),
				Compose(Prefix + "Consent form: ", string.Empty, consentUrl)
			};
		}

		/// <summary>
		/// The alert sent when the user has not answered a check-in or its reminder.
		/// </summary>
		/// <param name="userName">The name of the user who missed the check-in.</param>
		/// <param name="scheduledFor">The occurrence that was missed; shown in Ecuador local time.</param>
		/// <param name="latitude">The latitude of the last location the device reported for this check-in, or null.</param>
		/// <param name="longitude">The longitude of the last location the device reported for this check-in, or null.</param>
		/// <param name="capturedAt">When that location was reported; shown in Ecuador local time so the contact knows how old it is.</param>
		/// <returns>The message body: a map link when a location was reported, otherwise a request to call or visit.</returns>
		public static string MissedCheckIn(string userName, DateTimeOffset scheduledFor, double? latitude = null, double? longitude = null,
			DateTimeOffset? capturedAt = null)
		{
			string localTime = FormatLocalTime(scheduledFor);

			if (latitude is double lat && longitude is double lon && capturedAt is DateTimeOffset captured)
			{
				string map = string.Create(CultureInfo.InvariantCulture, $"https://maps.google.com/?q={lat:F5},{lon:F5}");
				return Compose(Prefix, userName, $" did not check in at {localTime}. Last known location ({FormatLocalTime(captured)}): {map}");
			}

			return Compose(Prefix, userName, $" did not check in at {localTime}. They may be having an emergency. Please call or visit them.");
		}

		/// <summary>
		/// The alert sent when the user presses the emergency button: a map link when the device supplied a location, otherwise the
		/// registered address.
		/// </summary>
		/// <param name="userName">The name of the user who pressed the button.</param>
		/// <param name="latitude">The latitude at the time of the press, or null.</param>
		/// <param name="longitude">The longitude at the time of the press, or null.</param>
		/// <param name="address">The user's registered address, used when no location was supplied.</param>
		/// <returns>The message body.</returns>
		public static string EmergencyActivation(string userName, double? latitude, double? longitude, string address)
		{
			const string Activated = " is NOT okay and is having an emergency.";

			if (latitude is double lat && longitude is double lon)
			{
				string map = string.Create(CultureInfo.InvariantCulture, $" Location: https://maps.google.com/?q={lat:F5},{lon:F5}");
				return Compose(Prefix, userName, Activated + map);
			}

			string lead = Prefix + Truncate(userName, MaxNameBeforeAddress) + Activated + " Address: ";
			return Compose(lead, address, string.Empty);
		}

		/// <summary>
		/// The emergency alert as one or two messages. With a location it is the single map-link message. Without one it carries the address
		/// and the landmarks: together when they fit in <see cref="MaxLength"/>, otherwise the landmarks follow in a second message,
		/// truncated to <see cref="MaxLength"/>.
		/// </summary>
		/// <param name="userName">The name of the user who pressed the button.</param>
		/// <param name="latitude">The latitude at the time of the press, or null.</param>
		/// <param name="longitude">The longitude at the time of the press, or null.</param>
		/// <param name="address">The user's registered address.</param>
		/// <param name="landmarks">The user's landmarks, or null.</param>
		/// <returns>The message bodies, in the order they must be sent.</returns>
		public static IReadOnlyList<string> EmergencyActivationMessages(string userName, double? latitude, double? longitude, string address,
			string? landmarks)
		{
			if ((latitude is not null && longitude is not null) || string.IsNullOrWhiteSpace(landmarks))
			{
				return new[] { EmergencyActivation(userName, latitude, longitude, address) };
			}

			const string Activated = " is NOT okay and is having an emergency.";
			string lead = Prefix + Truncate(userName, MaxNameBeforeAddress) + Activated + " Address: ";
			string trimmed = landmarks.Trim();
			string together = lead + address + " Landmarks: " + trimmed;
			if (together.Length <= MaxLength)
			{
				return new[] { together };
			}

			return new[] { Compose(lead, address, string.Empty), Compose(Prefix + "Landmarks: ", trimmed, string.Empty) };
		}

		/// <summary>
		/// The stand-down sent when the user cancels an emergency or an escalated missed check-in.
		/// </summary>
		/// <param name="userName">The name of the user who cancelled.</param>
		/// <returns>The message body.</returns>
		public static string EmergencyCancelled(string userName)
		{
			return Compose(Prefix, userName, " has indicated that they are no longer in an emergency status.");
		}

		/// <summary>
		/// Joins the three parts, truncating <paramref name="variable"/> so the whole fits in <see cref="MaxLength"/>.
		/// </summary>
		private static string Compose(string lead, string variable, string tail)
		{
			int room = MaxLength - lead.Length - tail.Length;
			if (room <= 0)
			{
				throw new InvalidOperationException($"Fixed SMS text ({lead.Length + tail.Length} chars) exceeds the {MaxLength} limit.");
			}

			return lead + Truncate(variable, room) + tail;
		}

		private static string FormatLocalTime(DateTimeOffset value)
		{
			return value.ToOffset(EscalationRules.LocalOffset).ToString("HH:mm", CultureInfo.InvariantCulture);
		}

		private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
	}
}
