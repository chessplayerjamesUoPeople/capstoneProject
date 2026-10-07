using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Azure Notification Hubs settings, bound from the <c>NotificationHub</c> configuration section. <see cref="ConnectionString"/> is a
	/// secret and belongs in Key Vault or an environment variable, never in appsettings.json.
	/// </summary>
	public sealed class NotificationHubOptions
	{
		/// <summary>The configuration section these options bind from.</summary>
		public const string SectionName = "NotificationHub";

		/// <summary>The name of the hub within its namespace, for example <c>amigoalert-hub</c>.</summary>
		public string HubName { get; set; } = string.Empty;

		/// <summary>
		/// The connection string of a hub-level access policy with the Listen and Send claims only: Listen to register devices, Send to push
		/// check-ins. Never the full-access policy, and never shipped in the mobile app.
		/// </summary>
		public string ConnectionString { get; set; } = string.Empty;
	}
}
