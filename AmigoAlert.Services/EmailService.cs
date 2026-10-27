using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <inheritdoc cref="IEmailService"/>
	/// <remarks>
	/// Sends through Azure Communication Services Email. A singleton, so the process has one <see cref="EmailClient"/>. The client is
	/// created on first use, like <see cref="NotificationHubPushService"/>'s, so hosts that never send email (the WebJob) need no email
	/// configuration, and a misconfiguration surfaces as a logged, failed send rather than a startup error.
	/// </remarks>
	internal sealed class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger) : IEmailService
	{
		private readonly Lazy<EmailClient> client = new Lazy<EmailClient>(() => CreateClient(options.Value),
			LazyThreadSafetyMode.PublicationOnly);

		/// <inheritdoc cref="IEmailService.SendAsync"/>
		public async Task<EmailDispatchResult> SendAsync(string toEmail, EmailContent content, CancellationToken ct = default)
		{
			try
			{
				// Started rather than Completed: the provider has accepted the message once the operation exists, and waiting for delivery
				// would hold the user on the "sending" screen for several seconds.
				EmailSendOperation operation = await client.Value.SendAsync(WaitUntil.Started, options.Value.SenderAddress, toEmail,
					content.Subject, content.Html, content.PlainText, ct);

				return new EmailDispatchResult(true, operation.Id);
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				// The address is personal data, so it is left out of the log.
				logger.LogError(exception, "Communication Services rejected the email \"{Subject}\".", content.Subject);
				return new EmailDispatchResult(false, null);
			}
		}

		private static EmailClient CreateClient(EmailOptions settings)
		{
			if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.SenderAddress))
			{
				throw new InvalidOperationException("Email:Endpoint and Email:SenderAddress must both be configured.");
			}

			return new EmailClient(new Uri(settings.Endpoint), new DefaultAzureCredential());
		}
	}
}
