using Azure;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Reads the Twilio credentials straight from Key Vault, so they never sit in app settings, environment variables or source control.
	/// </summary>
	public static class KeyVaultConfiguration
	{
		/// <summary>The configuration key holding the vault's URI (e.g. https://amigoalert-kv.vault.azure.net/).</summary>
		public const string VaultUriKey = "KeyVault:VaultUri";

		/// <summary>The vault's secret names and the configuration keys they fill.</summary>
		private static readonly Dictionary<string, string> KeysBySecretName = new()
		{
			["TwilioAccountSID"] = "Twilio:AccountSid",
			["TwilioAuthToken"] = "Twilio:AuthToken",
			["TwilioMessagingServiceSid"] = "Twilio:MessagingServiceSid"
		};

		/// <summary>
		/// Adds the Twilio secrets from the vault named by <see cref="VaultUriKey"/>, overriding every earlier source. Signs in with
		/// <see cref="DefaultAzureCredential"/>: the App Service's managed identity in Azure, and the developer's Visual Studio or
		/// az login account locally. Skipped when no vault is configured, or when the mock SMS service, which needs no credentials, is on.
		/// </summary>
		/// <remarks>
		/// Each secret is fetched by name rather than listing the vault, so the identity needs only Key Vault Secrets User on these secrets.
		/// They are read once at startup: after rotating one, restart the app.
		/// </remarks>
		/// <param name="configuration">The host's configuration, which must already contain its JSON and environment sources.</param>
		/// <returns>The same configuration manager, for chaining.</returns>
		/// <exception cref="InvalidOperationException">A secret is missing or cannot be read.</exception>
		public static IConfigurationManager AddAmigoAlertKeyVault(this IConfigurationManager configuration)
		{
			string? vaultUri = configuration[VaultUriKey];
			bool useMockSms = configuration.GetSection(TwilioOptions.SectionName).GetValue<bool>(nameof(TwilioOptions.UseMockSms));

			if (string.IsNullOrWhiteSpace(vaultUri) || useMockSms)
			{
				return configuration;
			}

			SecretClient client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
			Dictionary<string, string?> values = new Dictionary<string, string?>();

			foreach ((string secretName, string key) in KeysBySecretName)
			{
				try
				{
					values[key] = client.GetSecret(secretName).Value.Value;
				}
				catch (RequestFailedException exception)
				{
					// Fails startup instead of leaving the key blank, which Twilio would only report as an opaque 404 on the first send.
					throw new InvalidOperationException($"Could not read secret '{secretName}' from {vaultUri} (HTTP {exception.Status}). " +
						"Check that it exists and that this identity has the Key Vault Secrets User role.", exception);
				}
			}

			configuration.AddInMemoryCollection(values);

			return configuration;
		}
	}
}
