using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-11 to I-13: the emergency button and the consent that gates it. I-12 settles whether the API enforces consent or relies on the
	/// app disabling the button: it refuses with 409 and sends nothing. I-18 and I-19 cover the permanent link: withdrawing and granting
	/// again from it, and its death when the contact is replaced.
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public sealed class EmergencyTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
		/// <summary>Seed.sql's first two users, each with a contact who has granted consent.</summary>
		private static readonly Guid FirstUserId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

		private static readonly Guid SecondUserId = Guid.Parse("a1000000-0000-0000-0000-000000000002");

		private readonly HttpClient client = factory.CreateClient();

		public Task InitializeAsync()
		{
			return factory.ResetAsync();
		}

		public Task DisposeAsync()
		{
			return factory.RemoveRegisteredUsersAsync();
		}

		[Fact]
		public async Task I11_PressEmergencyButton_SetsEmergencyAndTextsTheContact()
		{
			HttpResponseMessage response = await client.PostAsJsonAsync($"api/users/{FirstUserId}/emergency",
				new ActivateEmergencyDto(0.3392, -78.2672));

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

			User user = await factory.InNewScopeAsync(db => db.Users.Include(u => u.EmergencyContacts).SingleAsync(u => u.Id == FirstUserId));
			EmergencyContact contact = Assert.Single(user.EmergencyContacts);
			Assert.Equal((byte)StatusType.Emergency, user.StatusId);

			StatusLog log = await factory.InNewScopeAsync(db => db.StatusLogs.SingleAsync(l => l.UserId == FirstUserId));
			Assert.Equal((byte)StatusType.Emergency, log.StatusId);
			Assert.Equal(0.3392, log.Location!.Y);
			Assert.Equal(-78.2672, log.Location.X);

			Sms sms = await factory.InNewScopeAsync(db => db.Sms.SingleAsync(m => m.EmergencyContactId == contact.Id));
			Assert.Equal((byte)SmsMessageTypeEnum.EmergencyActivation, sms.MessageTypeId);
			Assert.Equal(log.Id, sms.StatusLogId);

			MockSmsService.SentMessage sent = Assert.Single(factory.SentSms);
			Assert.Equal(contact.Phone, sent.ToPhone);
			Assert.NotEqual(user.Phone, sent.ToPhone);
		}

		[Fact]
		public async Task I12_EmergencyWithNoConsentedContact_IsRefusedAndSendsNothing()
		{
			// The awaiting-consent state, in which the app disables the button: the contact has been asked and has not answered.
			await factory.InNewScopeAsync(db => db.EmergencyContacts.Where(c => c.UserId == SecondUserId).ExecuteUpdateAsync(s => s
				.SetProperty(c => c.ConsentStatusId, (byte)ConsentStatusType.Pending)
				.SetProperty(c => c.ConsentToken, Guid.NewGuid())
				.SetProperty(c => c.ConsentTokenExpires, DateTimeOffset.UtcNow + ConsentService.TokenLifetime)
				.SetProperty(c => c.ConsentRespondedAt, (DateTimeOffset?)null)));

			HttpResponseMessage response = await client.PostAsJsonAsync($"api/users/{SecondUserId}/emergency",
				new ActivateEmergencyDto(null, null));

			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal((byte)StatusType.Okay, await factory.InNewScopeAsync(db => db.Users.Where(u => u.Id == SecondUserId)
				.Select(u => u.StatusId).SingleAsync()));
			Assert.False(await factory.InNewScopeAsync(db => db.StatusLogs.AnyAsync(l => l.UserId == SecondUserId)));
			Assert.False(await factory.InNewScopeAsync(db => db.Sms.AnyAsync(m => m.EmergencyContact.UserId == SecondUserId)));
			Assert.Empty(factory.SentSms);
		}

		[Fact]
		public async Task I13_ContactAcceptsConsent_EmergencyBecomesAvailable()
		{
			UserDto user = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());
			SetEmergencyContactDto contactRequest = new SetEmergencyContactDto("Integration Contact", "+593990009999");
			Assert.Equal(HttpStatusCode.Created,
				(await client.PostAsJsonAsync($"api/users/{user.Id}/emergency-contact", contactRequest)).StatusCode);

			// The token the contact receives, taken from the link in the SMS rather than the database.
			MockSmsService.SentMessage request = factory.SentSms.Last(); // the second SMS carries the link
			Guid token = Guid.ParseExact(Regex.Match(request.Body, "[0-9a-f]{32}").Value, "N");

			Assert.Equal(HttpStatusCode.Conflict,
				(await client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);

			HttpResponseMessage consent = await client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(true));

			Assert.Equal(HttpStatusCode.NoContent, consent.StatusCode);
			EmergencyContact contact = await factory.InNewScopeAsync(db => db.EmergencyContacts.SingleAsync(c => c.UserId == user.Id));
			Assert.Equal((byte)ConsentStatusType.Granted, contact.ConsentStatusId);
			Assert.NotNull(contact.ConsentRespondedAt);
			Assert.Equal(token, contact.ConsentToken);
			Assert.Null(contact.ConsentTokenExpires);

			HttpResponseMessage emergency = await client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null));

			Assert.Equal(HttpStatusCode.NoContent, emergency.StatusCode);
			Assert.Equal((byte)StatusType.Emergency, await factory.InNewScopeAsync(db => db.Users.Where(u => u.Id == user.Id)
				.Select(u => u.StatusId).SingleAsync()));
			// Two consent SMS (notice and link), then one alert carrying address and landmarks.
			Assert.Collection(factory.SentSms,
				m => Assert.Equal(contactRequest.Phone, m.ToPhone),
				m => Assert.Equal(contactRequest.Phone, m.ToPhone),
				m => Assert.Equal(contactRequest.Phone, m.ToPhone));
		}

		[Fact]
		public async Task I18_ContactWithdrawsConsent_EmergencyIsRefusedUntilTheyGrantAgain()
		{
			UserDto user = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());
			SetEmergencyContactDto contactRequest = new SetEmergencyContactDto("Withdrawing Contact", "+593990008888");
			Assert.Equal(HttpStatusCode.Created,
				(await client.PostAsJsonAsync($"api/users/{user.Id}/emergency-contact", contactRequest)).StatusCode);
			MockSmsService.SentMessage request = factory.SentSms.Last(); // the last SMS carries the link
			Guid token = Guid.ParseExact(Regex.Match(request.Body, "[0-9a-f]{32}").Value, "N");

			Assert.Equal(HttpStatusCode.NoContent,
				(await client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(true))).StatusCode);

			// The same link, unchanged, still resolves and offers the contact a choice.
			ConsentRequestDto granted = (await client.GetFromJsonAsync<ConsentRequestDto>($"api/consent/{token}"))!;
			Assert.Equal((ConsentStatusType.Granted, true), (granted.ConsentStatus, granted.CanRespond));

			Assert.Equal(HttpStatusCode.NoContent,
				(await client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(false))).StatusCode);

			EmergencyContact contact = await factory.InNewScopeAsync(db => db.EmergencyContacts.SingleAsync(c => c.UserId == user.Id));
			Assert.Equal((byte)ConsentStatusType.Revoked, contact.ConsentStatusId);
			Assert.NotNull(contact.ConsentRespondedAt);
			Assert.Equal(token, contact.ConsentToken);

			// Withdrawing twice records nothing the second time.
			Assert.Equal(HttpStatusCode.NotFound,
				(await client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(false))).StatusCode);

			int smsBefore = factory.SentSms.Count;
			Assert.Equal(HttpStatusCode.Conflict,
				(await client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);
			Assert.Equal(smsBefore, factory.SentSms.Count);

			// Granting again from the same link restores the contact.
			Assert.Equal(HttpStatusCode.NoContent,
				(await client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(true))).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent,
				(await client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);
		}

		[Fact]
		public async Task I19_ReplacedContact_OldLinkStopsWorking()
		{
			UserDto user = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());
			Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"api/users/{user.Id}/emergency-contact",
				new SetEmergencyContactDto("Old Contact", "+593990008877"))).StatusCode);
			Guid oldToken = Guid.ParseExact(Regex.Match(factory.SentSms.Last().Body, "[0-9a-f]{32}").Value, "N");
			Assert.Equal(HttpStatusCode.NoContent,
				(await client.PutAsJsonAsync($"api/consent/{oldToken}", new RespondToConsentDto(true))).StatusCode);

			Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"api/users/{user.Id}/emergency-contact",
				new SetEmergencyContactDto("New Contact", "+593990008866"))).StatusCode);

			Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/consent/{oldToken}")).StatusCode);
			Assert.Equal(HttpStatusCode.NotFound,
				(await client.PutAsJsonAsync($"api/consent/{oldToken}", new RespondToConsentDto(false))).StatusCode);
		}
	}
}
