using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Azure Communication Services Email settings, bound from the <c>Email</c> configuration section. No secret is needed: the service
	/// signs in with the app's managed identity (or the developer's az login account) through <c>DefaultAzureCredential</c>.
	/// </summary>
	public sealed class EmailOptions
	{
		/// <summary>The configuration section these options bind from.</summary>
		public const string SectionName = "Email";

		/// <summary>The Communication Services resource endpoint, for example <c>https://amigoalert-acs.communication.azure.com/</c>.</summary>
		public string Endpoint { get; set; } = string.Empty;

		/// <summary>
		/// The verified sender address of a domain connected to the Communication Services resource, for example
		/// <c>DoNotReply@xxxxxxxx.azurecomm.net</c>.
		/// </summary>
		public string SenderAddress { get; set; } = string.Empty;

		/// <summary>
		/// When true, <see cref="MockEmailService"/> is used instead of Communication Services: messages are logged and recorded but never
		/// sent, and the other settings here are ignored. For development and testing only.
		/// </summary>
		public bool UseMockEmail { get; set; }
	}
}
