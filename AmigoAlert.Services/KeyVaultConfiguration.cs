using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace AmigoAlert.Services
{
	/// <summary>Loads the Twilio credentials from Azure Key Vault into configuration, for both the API and the WebJob.</summary>
	public static class KeyVaultConfiguration
	{
		/// <summary>
		/// Adds Key Vault as a configuration source when <c>KeyVault:VaultUri</c> is set, authenticating with
		/// <see cref="DefaultAzureCredential"/> (the App Service's managed identity in Azure, your signed-in account locally). Does nothing
		/// when the key is empty, so local runs with mock SMS need no vault access.
		/// </summary>
		public static IConfigurationManager AddAmigoAlertKeyVault(this IConfigurationManager configuration)
		{
			string? vaultUri = configuration["KeyVault:VaultUri"];
			if (!string.IsNullOrWhiteSpace(vaultUri))
			{
				configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential(), new TwilioSecretManager());
			}

			return configuration;
		}

		/// <summary>Loads only the three Twilio secrets, mapping their vault names onto the <c>Twilio</c> options keys.</summary>
		private sealed class TwilioSecretManager : KeyVaultSecretManager
		{
			private static readonly Dictionary<string, string> Keys = new(StringComparer.OrdinalIgnoreCase)
			{
				["TwilioAccountSid"] = "Twilio:AccountSid",
				["TwilioAuthToken"] = "Twilio:AuthToken",
				["TwilioMessagingServiceSid"] = "Twilio:MessagingServiceSid",
			};

			public override bool Load(SecretProperties secret) => Keys.ContainsKey(secret.Name);

			public override string GetKey(KeyVaultSecret secret) => Keys[secret.Name];
		}
	}
}
