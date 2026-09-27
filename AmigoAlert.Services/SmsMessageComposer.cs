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
	/// text leaves. The fixed text is Spanish written without accents: á, í, ó and ú are not in the GSM-7 alphabet and would force UCS-2,
	/// whose single-segment limit is 70 characters.
	/// </remarks>
	public static class SmsMessageComposer
	{
		/// <summary>The maximum length of one SMS on Movistar Ecuador.</summary>
		public const int MaxLength = 160;

		/// <summary>The longest name placed ahead of an address, so the address keeps enough room to be useful.</summary>
		private const int MaxNameBeforeAddress = 40;

		private const string Prefix = "AmigoAlert: ";

		/// <summary>
		/// The request asking a new contact to agree to receive alerts, linking to the consent page.
		/// </summary>
		/// <param name="userName">The name of the user the contact would receive alerts for.</param>
		/// <param name="consentUrl">The full consent page URL, including the token.</param>
		/// <returns>The message body.</returns>
		public static string ConsentRequest(string userName, string consentUrl)
		{
			return Compose(Prefix, userName, $" le pide ser su contacto de emergencia: {consentUrl}");
		}

		/// <summary>
		/// The alert sent when the user has not answered a check-in or its reminder.
		/// </summary>
		/// <param name="userName">The name of the user who missed the check-in.</param>
		/// <param name="scheduledFor">The occurrence that was missed; shown in Ecuador local time.</param>
		/// <returns>The message body.</returns>
		public static string MissedCheckIn(string userName, DateTimeOffset scheduledFor)
		{
			string localTime = scheduledFor.ToOffset(EscalationRules.LocalOffset).ToString("HH:mm", CultureInfo.InvariantCulture);

			return Compose(Prefix, userName, $" no respondio al control de las {localTime}. Por favor llame o visite a esta persona.");
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
			const string Activated = " activo el boton de emergencia.";

			if (latitude is double lat && longitude is double lon)
			{
				string map = string.Create(CultureInfo.InvariantCulture, $" Ubicacion: https://maps.google.com/?q={lat:F5},{lon:F5}");
				return Compose(Prefix, userName, Activated + map);
			}

			string lead = Prefix + Truncate(userName, MaxNameBeforeAddress) + Activated + " Direccion: ";
			return Compose(lead, address, string.Empty);
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

		private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
	}
}
