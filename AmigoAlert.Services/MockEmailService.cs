using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// A stand-in for <see cref="EmailService"/> that never contacts Communication Services: every message is accepted and written to the
	/// log, so registration and log in can be exercised without an email domain, and a developer can read the code from the API console.
	/// </summary>
	/// <remarks>
	/// Registered as a singleton in place of <see cref="EmailService"/> when <see cref="EmailOptions.UseMockEmail"/> is true, for example
	/// with the <c>Email__UseMockEmail=true</c> environment variable. One instance serves every request, so <see cref="Sent"/> holds every
	/// message the process has sent.
	/// </remarks>
	public sealed class MockEmailService(TimeProvider timeProvider, ILogger<MockEmailService> logger) : IEmailService
	{
		/// <summary>The prefix of every mock provider message identifier.</summary>
		public const string IdPrefix = "mock-";

		/// <summary>One message accepted by <see cref="SendAsync"/>.</summary>
		/// <param name="ToEmail">The recipient's address.</param>
		/// <param name="Content">The subject and bodies.</param>
		/// <param name="SentAt">When the message was accepted, from the injected <see cref="TimeProvider"/>.</param>
		public sealed record SentMessage(string ToEmail, EmailContent Content, DateTimeOffset SentAt);

		private readonly List<SentMessage> sent = [];
		private readonly Lock sync = new Lock();

		/// <summary>A snapshot of every message accepted since construction or the last <see cref="Clear"/>, oldest first.</summary>
		public IReadOnlyList<SentMessage> Sent
		{
			get
			{
				lock (sync)
				{
					return sent.ToList();
				}
			}
		}

		/// <summary>Forgets the messages recorded in <see cref="Sent"/>.</summary>
		public void Clear()
		{
			lock (sync)
			{
				sent.Clear();
			}
		}

		/// <inheritdoc cref="IEmailService.SendAsync"/>
		public Task<EmailDispatchResult> SendAsync(string toEmail, EmailContent content, CancellationToken ct = default)
		{
			string id = IdPrefix + Guid.NewGuid().ToString("N");

			lock (sync)
			{
				sent.Add(new SentMessage(toEmail, content, timeProvider.GetUtcNow()));
			}

			logger.LogInformation("Mock email {Id} to {ToEmail}: {Subject}", id, toEmail, content.Subject);

			return Task.FromResult(new EmailDispatchResult(true, id));
		}
	}
}
