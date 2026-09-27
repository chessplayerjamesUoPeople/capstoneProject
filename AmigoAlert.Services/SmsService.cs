using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.Enums;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Security;
using Twilio.Types;

namespace AmigoAlert.Services
{
	/// <inheritdoc cref="ISmsService"/>
	internal sealed class SmsService(ITwilioRestClient twilioClient, ISmsRepository smsRepository, IOptions<TwilioOptions> options,
		TimeProvider timeProvider) : ISmsService
	{
		/// <inheritdoc cref="ISmsService.SendAsync"/>
		public async Task<SmsDispatchResult> SendAsync(Guid emergencyContactId, string toPhone, string body, SmsMessageTypeEnum messageType,
			long? statusLogId, CancellationToken ct = default)
		{
			if (body.Length > SmsMessageComposer.MaxLength)
			{
				throw new ArgumentException($"The SMS body is {body.Length} characters; the limit is {SmsMessageComposer.MaxLength}.",
					nameof(body));
			}

			TwilioOptions settings = options.Value;
			CreateMessageOptions request = new CreateMessageOptions(new PhoneNumber(toPhone))
			{
				MessagingServiceSid = settings.MessagingServiceSid,
				Body = body,
				// Replaces look-alike Unicode (curly quotes, accented vowels in names) with GSM-7 equivalents, so a message is not
				// re-encoded as UCS-2, where the single-segment limit drops from 160 to 70 characters.
				SmartEncoded = true,
				StatusCallback = string.IsNullOrEmpty(settings.StatusCallbackUrl) ? null : new Uri(settings.StatusCallbackUrl)
			};

			MessageResource message;
			try
			{
				message = await MessageResource.CreateAsync(request, twilioClient);
			}
			catch (ApiException exception)
			{
				return new SmsDispatchResult(false, null, exception.Code);
			}
			catch (TwilioException)
			{
				return new SmsDispatchResult(false, null, null);
			}

			await smsRepository.AddAsync(new Sms
			{
				EmergencyContactId = emergencyContactId,
				StatusLogId = statusLogId,
				TimeSent = timeProvider.GetUtcNow(),
				TwilioSid = message.Sid,
				Status = message.Status?.ToString(),
				ErrorCode = message.ErrorCode,
				MessageTypeId = (byte)messageType
			}, ct);

			return new SmsDispatchResult(true, message.Sid, message.ErrorCode);
		}

		/// <inheritdoc cref="ISmsService.IsValidStatusCallback"/>
		public bool IsValidStatusCallback(IDictionary<string, string> parameters, string? signature)
		{
			TwilioOptions settings = options.Value;

			if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(settings.StatusCallbackUrl))
			{
				return false;
			}

			RequestValidator validator = new RequestValidator(settings.AuthToken);
			return validator.Validate(settings.StatusCallbackUrl, parameters, signature);
		}
	}
}
