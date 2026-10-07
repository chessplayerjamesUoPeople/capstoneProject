using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// A stand-in for <see cref="SmsService"/> that never contacts Twilio: every message is accepted, written to the log, and recorded as
	/// an <c>Sms</c> row, so the escalation and consent flows can be exercised without credentials, cost or real recipients.
	/// </summary>
	/// <remarks>
	/// Not registered anywhere: <see cref="SmsService"/> is the only <see cref="ISmsService"/> in use. To use this one, register it in
	/// <c>DependencyInjection</c> in place of <see cref="SmsService"/>. Recorded rows carry a
	/// <see cref="SidPrefix"/> SID so they can be told apart from real Twilio messages, whose SIDs start with "SM".
	/// </remarks>
	internal sealed class MockSmsService(ISmsRepository smsRepository, TimeProvider timeProvider, ILogger<MockSmsService> logger)
		: ISmsService
	{
		/// <summary>The prefix of every mock SID. Followed by 32 hex digits, it has the same 34-character length as a Twilio SID.</summary>
		public const string SidPrefix = "MK";

		/// <summary>The delivery status recorded against mock messages.</summary>
		private const string MockStatus = "mock";

		/// <inheritdoc cref="ISmsService.SendAsync"/>
		public async Task<SmsDispatchResult> SendAsync(Guid emergencyContactId, string toPhone, string body, SmsMessageTypeEnum messageType,
			long? statusLogId, CancellationToken ct = default)
		{
			// Enforced here as well, so a body that would be rejected in production fails the same way in testing.
			if (body.Length > SmsMessageComposer.MaxLength)
			{
				throw new ArgumentException($"The SMS body is {body.Length} characters; the limit is {SmsMessageComposer.MaxLength}.",
					nameof(body));
			}

			string sid = SidPrefix + Guid.NewGuid().ToString("N");

			await smsRepository.AddAsync(new Sms
			{
				EmergencyContactId = emergencyContactId,
				StatusLogId = statusLogId,
				TimeSent = timeProvider.GetUtcNow(),
				TwilioSid = sid,
				Status = MockStatus,
				ErrorCode = null,
				MessageTypeId = (byte)messageType
			}, ct);

			logger.LogInformation("Mock SMS {Sid} ({MessageType}) to {ToPhone}: {Body}", sid, messageType, toPhone, body);

			return new SmsDispatchResult(true, sid, null);
		}

		/// <inheritdoc cref="ISmsService.IsValidStatusCallback"/>
		/// <remarks>Mock messages never reach Twilio, so no genuine callback can exist for them.</remarks>
		public bool IsValidStatusCallback(IDictionary<string, string> parameters, string? signature)
		{
			return false;
		}
	}
}
