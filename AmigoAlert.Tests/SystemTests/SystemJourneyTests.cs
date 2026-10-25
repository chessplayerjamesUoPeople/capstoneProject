using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using AmigoAlert.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace AmigoAlert.Tests.SystemTests
{
	/// <summary>
	/// S-01 to S-10: end-to-end journeys through the API, the consent flow and the WebJob, against SQL Server with the mock SMS service.
	/// Each test is one row of Documentation/Testing/SystemTestCases.md and names the requirements it covers. Database reads are only
	/// used to check what no public surface exposes (location rows, inactive contacts, Sms rows).
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	[Trait("Level", "System")]
	public sealed class SystemJourneyTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
		private readonly SystemTestDriver driver = new SystemTestDriver(factory);

		private HttpClient Client => driver.Client;

		public Task InitializeAsync()
		{
			return factory.ResetAsync();
		}

		public Task DisposeAsync()
		{
			return factory.RemoveRegisteredUsersAsync();
		}

		/// <summary>US-01, US-03, US-04, US-05, US-07, US-15, DR-02.</summary>
		[Fact]
		public async Task S01_OnboardingThroughConsent_UnlocksCheckInsAndEmergency()
		{
			// DR-02: the app normalises what the user types; the API stores and accepts only E.164.
			Assert.True(PhoneNumberFormat.TryNormalize("099 000 7777", out string contactPhone));
			Assert.Equal("+593990007777", contactPhone);
			CreateUserDto request = driver.NewUser();
			Assert.Equal(HttpStatusCode.BadRequest,
				(await Client.PostAsJsonAsync("api/users", request with { Phone = "0980009999" })).StatusCode);

			UserDto user = await driver.RegisterAsync(request);
			Assert.Equal(StatusType.Okay, user.Status);
			Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"api/users/{user.Id}/emergency-contact")).StatusCode);

			// US-03: the contact is texted the link, whole. That the user's name survives is S11, held back by defect D-01.
			Guid token = await driver.AddContactAsync(user.Id, new SetEmergencyContactDto("System Contact", contactPhone));
			MockSmsService.SentMessage consentSms = factory.SentSms.Last();
			Assert.EndsWith(token.ToString("N"), consentSms.Body);
			Assert.Equal(contactPhone, (await driver.GetContactAsync(user.Id)).Phone);

			// US-04: the link resolves to an open request for this user.
			ConsentRequestDto consent = (await Client.GetFromJsonAsync<ConsentRequestDto>($"api/consent/{token}"))!;
			Assert.Equal((request.Name, "System Contact", ConsentStatusType.Pending, true),
				(consent.UserName, consent.ContactName, consent.ConsentStatus, consent.CanRespond));

			// US-07: until the contact answers, no check-in is sent and the emergency button is refused.
			await driver.RunWebJobAsync(driver.Occurrence);
			Assert.Empty(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(HttpStatusCode.Conflict,
				(await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);

			// US-05: opting in takes effect at once, and the link cannot be used twice.
			Assert.Equal(HttpStatusCode.NoContent, (await driver.AnswerConsentAsync(token, granted: true)).StatusCode);
			Assert.Equal(ConsentStatusType.Granted, (await driver.GetContactAsync(user.Id)).ConsentStatus);
			Assert.Equal(HttpStatusCode.NotFound, (await driver.AnswerConsentAsync(token, granted: false)).StatusCode);

			// US-15: the next WebJob run sends the check-in.
			await driver.RunWebJobAsync(driver.Occurrence);
			CheckInDto checkIn = Assert.Single(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(driver.Occurrence, checkIn.ScheduledFor);
			Assert.Equal(StatusType.Pending1, (await driver.GetUserAsync(user.Id)).Status);

			Assert.All(factory.SentSms, SystemTestDriver.AssertSingleSegment);
		}

		/// <summary>US-06, US-08, US-09.</summary>
		[Fact]
		public async Task S02_ContactDeclines_UserStaysLockedUntilANewContactConsents()
		{
			UserDto user = await driver.RegisterAsync();
			SetEmergencyContactDto declined = new SetEmergencyContactDto("Declining Contact", "+593990006666");
			Guid token = await driver.AddContactAsync(user.Id, declined);

			// US-06: opting out is recorded, and the contact is never messaged about the user.
			Assert.Equal(HttpStatusCode.NoContent, (await driver.AnswerConsentAsync(token, granted: false)).StatusCode);
			Assert.Equal(ConsentStatusType.Rejected, (await driver.GetContactAsync(user.Id)).ConsentStatus);

			IReadOnlyList<MockSmsService.SentMessage> afterDecline = factory.SentSms;
			await driver.RunWebJobAsync(driver.Occurrence);
			Assert.Empty(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(HttpStatusCode.Conflict,
				(await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);

			// US-09: submitting the declined number again does not ask them again; the contact stays opted out.
			HttpResponseMessage again = await Client.PutAsJsonAsync($"api/users/{user.Id}/emergency-contact",
				declined with { Name = "Declining Contact Again" });
			Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
			Assert.Equal(ConsentStatusType.Rejected, (await driver.GetContactAsync(user.Id)).ConsentStatus);
			Assert.Empty(driver.SmsSince(afterDecline));

			// US-08: a new contact gets a new request, and consenting unlocks the app.
			Guid newToken = await driver.ReplaceContactAsync(user.Id, new SetEmergencyContactDto("New Contact", "+593990005555"));
			Assert.Equal(ConsentStatusType.Pending, (await driver.GetContactAsync(user.Id)).ConsentStatus);
			Assert.Equal(HttpStatusCode.NoContent, (await driver.AnswerConsentAsync(newToken, granted: true)).StatusCode);

			await driver.RunWebJobAsync(driver.Occurrence);
			Assert.Single(await driver.GetCheckInsAsync(user.Id));
			Assert.DoesNotContain(driver.SmsSince(afterDecline), m => m.ToPhone == declined.Phone);
		}

		/// <summary>US-07: consent requested but not answered, checked from the WebJob's side over a whole occurrence.</summary>
		[Fact]
		public async Task S03_AwaitingConsent_NoCheckInsRemindersOrAlerts()
		{
			UserDto user = await driver.RegisterAsync();
			await driver.AddContactAsync(user.Id, new SetEmergencyContactDto("Silent Contact", "+593990004444"));
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			foreach (DateTimeOffset run in new[] { driver.Occurrence, driver.ReminderDue, driver.EscalationDue })
			{
				await driver.RunWebJobAsync(run);
			}

			Assert.Empty(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(StatusType.Okay, (await driver.GetUserAsync(user.Id)).Status);
			Assert.Equal(HttpStatusCode.Conflict,
				(await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);
			Assert.Empty(driver.SmsSince(before));
		}

		/// <summary>US-10, US-12, US-15, US-16, NF-11.</summary>
		[Fact]
		public async Task S04_CheckInAnswered_ReturnsToOkayDiscardsLocationAndNeverEscalates()
		{
			(UserDto user, _) = await driver.ConsentedUserAsync();
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			await driver.RunWebJobAsync(driver.Occurrence);
			CheckInDto checkIn = Assert.Single(await driver.GetCheckInsAsync(user.Id));

			// US-12: the home screen reads a pending check-in from the user's status.
			Assert.Equal(StatusType.Pending1, (await driver.GetUserAsync(user.Id)).Status);

			// The device reports where it was when the notification arrived.
			Assert.Equal(HttpStatusCode.NoContent,
				(await Client.PutAsJsonAsync($"api/checkins/{checkIn.Id}/location", new ActivateEmergencyDto(0.3392, -78.2672))).StatusCode);
			Assert.True(await HasLocationAsync(user.Id));

			// US-16: "I'm okay" stands everything down.
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"api/checkins/{checkIn.Id}/response",
				new RespondToCheckInDto(CheckInResponseType.Okay))).StatusCode);
			Assert.Equal(StatusType.Okay, (await driver.GetUserAsync(user.Id)).Status);
			Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"api/checkins/{checkIn.Id}/response",
				new RespondToCheckInDto(CheckInResponseType.Okay))).StatusCode);

			// NF-11: a location is kept only while it could still be needed for an alert.
			Assert.False(await HasLocationAsync(user.Id));

			await driver.RunWebJobAsync(driver.ReminderDue);
			await driver.RunWebJobAsync(driver.EscalationDue);

			CheckInDto answered = Assert.Single(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(CheckInResponseType.Okay, answered.Response);
			Assert.Equal(StatusType.Okay, (await driver.GetUserAsync(user.Id)).Status);
			Assert.Empty(driver.SmsSince(before));
		}

		/// <summary>US-13, US-19, US-20, US-21, US-22, with the location the device reported.</summary>
		[Fact]
		public async Task S05_MissedCheckIn_RemindsThenEscalatesWithLocationThenCancels()
		{
			(UserDto user, SetEmergencyContactDto contact) = await driver.ConsentedUserAsync();
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			await driver.RunWebJobAsync(driver.Occurrence);
			CheckInDto first = Assert.Single(await driver.GetCheckInsAsync(user.Id));
			Assert.Equal(HttpStatusCode.NoContent,
				(await Client.PutAsJsonAsync($"api/checkins/{first.Id}/location", new ActivateEmergencyDto(0.3392, -78.2672))).StatusCode);

			// Not yet due: one minute short of the reminder.
			await driver.RunWebJobAsync(driver.ReminderDue.AddMinutes(-1));
			Assert.Single(await driver.GetCheckInsAsync(user.Id));

			// US-19: the reminder, ten minutes after the first notification.
			await driver.RunWebJobAsync(driver.ReminderDue);
			Assert.Equal(2, (await driver.GetCheckInsAsync(user.Id)).Count);
			Assert.Equal(StatusType.Pending2, (await driver.GetUserAsync(user.Id)).Status);
			Assert.Empty(driver.SmsSince(before));

			// US-20: ten minutes after the reminder, the contact is texted once, with a map link.
			await driver.RunWebJobAsync(driver.EscalationDue);
			await driver.RunWebJobAsync(driver.EscalationDue.AddMinutes(1));

			Assert.Equal(StatusType.Escalated, (await driver.GetUserAsync(user.Id)).Status);
			MockSmsService.SentMessage alert = Assert.Single(driver.SmsSince(before));
			Assert.Equal(contact.Phone, alert.ToPhone);
			Assert.Contains(user.Name, alert.Body);
			Assert.Contains("https://maps.google.com/?q=0.33920,-78.26720", alert.Body);

			// US-21 and US-22: cancelling returns the user to okay and tells the contact to stand down.
			await CancelAndAssertStandDownAsync(user, contact);
		}

		/// <summary>US-20 when the device never reported a location.</summary>
		[Fact]
		public async Task S05b_MissedCheckInWithoutLocation_AsksTheContactToCallOrVisit()
		{
			(UserDto user, SetEmergencyContactDto contact) = await driver.ConsentedUserAsync();
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			foreach (DateTimeOffset run in new[] { driver.Occurrence, driver.ReminderDue, driver.EscalationDue })
			{
				await driver.RunWebJobAsync(run);
			}

			MockSmsService.SentMessage alert = Assert.Single(driver.SmsSince(before));
			Assert.Equal(contact.Phone, alert.ToPhone);
			Assert.Contains("may be having an emergency", alert.Body);
			Assert.DoesNotContain("maps.google.com", alert.Body);
			Assert.Equal(SmsMessageTypeEnum.MissedCheckInEscalation, await LatestSmsTypeAsync(user.Id));
		}

		/// <summary>US-14, US-17, US-18, US-21, US-22.</summary>
		[Fact]
		public async Task S06_EmergencyButton_AlertsTheContactAndCanBeCancelled()
		{
			(UserDto user, SetEmergencyContactDto contact) = await driver.ConsentedUserAsync();
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			// US-17 and US-18: with a location, the alert carries a map link.
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency",
				new ActivateEmergencyDto(0.2297, -78.2612))).StatusCode);
			Assert.Equal(StatusType.Emergency, (await driver.GetUserAsync(user.Id)).Status);

			MockSmsService.SentMessage alert = Assert.Single(driver.SmsSince(before));
			Assert.Equal(contact.Phone, alert.ToPhone);
			Assert.Contains(user.Name, alert.Body);
			Assert.Contains("NOT okay", alert.Body);
			Assert.Contains("https://maps.google.com/?q=0.22970,-78.26120", alert.Body);
			Assert.Equal(SmsMessageTypeEnum.EmergencyActivation, await LatestSmsTypeAsync(user.Id));

			await CancelAndAssertStandDownAsync(user, contact);

			// Without a location, the alert falls back to the registered address.
			IReadOnlyList<MockSmsService.SentMessage> second = factory.SentSms;
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency",
				new ActivateEmergencyDto(null, null))).StatusCode);
			MockSmsService.SentMessage fallback = Assert.Single(driver.SmsSince(second));
			Assert.Contains(user.Address, fallback.Body);
		}

		/// <summary>US-23, and NF-11's retention of consent records.</summary>
		[Fact]
		public async Task S07_UpdateInformation_NewContactNumberRevertsToPendingConsent()
		{
			(UserDto user, SetEmergencyContactDto contact) = await driver.ConsentedUserAsync();
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			// The user's own details, and the contact's name, change without touching consent.
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"api/users/{user.Id}",
				new UpdateUserDto("System User Renamed", user.Phone, "11 New St., Cotacachi, Ecuador", "Green gate"))).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync($"api/users/{user.Id}/emergency-contact",
				contact with { Name = "System Contact Renamed" })).StatusCode);

			UserDto renamed = await driver.GetUserAsync(user.Id);
			EmergencyContactDto sameContact = await driver.GetContactAsync(user.Id);
			Assert.Equal(("System User Renamed", "11 New St., Cotacachi, Ecuador"), (renamed.Name, renamed.Address));
			Assert.Equal(("System Contact Renamed", ConsentStatusType.Granted), (sameContact.Name, sameContact.ConsentStatus));
			Assert.Empty(driver.SmsSince(before));

			// A new number has agreed to nothing: back to pending, with a new request naming the renamed user.
			Guid token = await driver.ReplaceContactAsync(user.Id, new SetEmergencyContactDto("Other Contact", "+593990003333"));
			Assert.All(driver.SmsSince(before), m => Assert.Equal("+593990003333", m.ToPhone));
			Assert.Equal(2, driver.SmsSince(before).Count);
			Assert.Equal(ConsentStatusType.Pending, (await driver.GetContactAsync(user.Id)).ConsentStatus);
			Assert.Equal(HttpStatusCode.Conflict,
				(await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);

			// The old contact's consent is kept for audit, inactive.
			EmergencyContact old = await factory.InNewScopeAsync(db => db.EmergencyContacts
				.SingleAsync(c => c.UserId == user.Id && c.Phone == contact.Phone));
			Assert.False(old.IsActive);
			Assert.Equal((byte)ConsentStatusType.Granted, old.ConsentStatusId);

			Assert.Equal(HttpStatusCode.NoContent, (await driver.AnswerConsentAsync(token, granted: true)).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent,
				(await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency", new ActivateEmergencyDto(null, null))).StatusCode);
			Assert.Equal("+593990003333", driver.SmsSince(before).Last().ToPhone);
		}

		/// <summary>NF-11: the WebJob's purge removes locations older than 90 days and keeps newer ones.</summary>
		[Fact]
		public async Task S08_WebJobRun_PurgesLocationsOlderThan90Days()
		{
			(UserDto user, _) = await driver.ConsentedUserAsync();
			DateTimeOffset now = DateTimeOffset.UtcNow;

			(long expired, long retained) = await factory.InNewScopeAsync(async db =>
			{
				StatusLog old = NewLocationLog(user.Id, now.AddDays(-91));
				StatusLog recent = NewLocationLog(user.Id, now.AddDays(-89));
				db.StatusLogs.AddRange(old, recent);
				await db.SaveChangesAsync();

				return (old.Id, recent.Id);
			});

			await driver.RunWebJobAsync(driver.Occurrence);

			Assert.Null(await factory.InNewScopeAsync(db => db.StatusLogs.Where(l => l.Id == expired).Select(l => l.Location).SingleAsync()));
			Assert.NotNull(await factory.InNewScopeAsync(db => db.StatusLogs.Where(l => l.Id == retained).Select(l => l.Location)
				.SingleAsync()));
		}

		/// <summary>
		/// NF-13: the longest name and address the API accepts still produce single-segment messages on every path that texts the contact.
		/// </summary>
		[Fact]
		public async Task S09_LongestAcceptedNameAndAddress_EverySmsFitsOneSegment()
		{
			CreateUserDto longest = driver.NewUser() with { Name = new string('N', 50), Address = new string('A', 200) };
			(UserDto user, _) = await driver.ConsentedUserAsync(longest);

			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency",
				new ActivateEmergencyDto(null, null))).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"api/users/{user.Id}/emergency/cancel", null)).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"api/users/{user.Id}/emergency",
				new ActivateEmergencyDto(-0.18065, -78.46784))).StatusCode);
			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"api/users/{user.Id}/emergency/cancel", null)).StatusCode);

			foreach (DateTimeOffset run in new[] { driver.Occurrence, driver.ReminderDue, driver.EscalationDue })
			{
				await driver.RunWebJobAsync(run);
			}

			// Consent request (two), two activations (the address-only one adds a landmarks SMS), two stand-downs, one escalation.
			Assert.Equal(8, factory.SentSms.Count);
			Assert.All(factory.SentSms, SystemTestDriver.AssertSingleSegment);

			List<string?> sids = await factory.InNewScopeAsync(db => db.Sms.Where(m => m.EmergencyContact.UserId == user.Id)
				.Select(m => m.TwilioSid).ToListAsync());
			Assert.Equal(8, sids.Count);
			Assert.All(sids, sid => Assert.StartsWith(MockSmsService.SidPrefix, sid));
		}

		/// <summary>The unauthenticated Twilio callback refuses anything without a valid signature.</summary>
		[Fact]
		public async Task S10_UnsignedDeliveryCallback_IsForbidden()
		{
			FormUrlEncodedContent form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["MessageSid"] = "SM00000000000000000000000000000000",
				["MessageStatus"] = "delivered"
			});

			HttpResponseMessage response = await Client.PostAsync("api/sms/status", form);

			Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
		}

		/// <summary>
		/// US-03 with the consent page URL the API is deployed with. Fails today: the URL leaves about five characters for the name, so
		/// the contact cannot tell who is asking.
		/// </summary>
		[Fact(Skip = "D-01: the consent SMS truncates the user's name to fit the production consent URL. See TestReport.md.")]
		public async Task S11_ConsentRequest_NamesTheUser()
		{
			CreateUserDto request = driver.NewUser();
			UserDto user = await driver.RegisterAsync(request);

			await driver.AddContactAsync(user.Id, new SetEmergencyContactDto("System Contact", "+593990007777"));

			Assert.Contains(request.Name, factory.SentSms.First().Body);
		}

		private async Task CancelAndAssertStandDownAsync(UserDto user, SetEmergencyContactDto contact)
		{
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"api/users/{user.Id}/emergency/cancel", null)).StatusCode);
			Assert.Equal(StatusType.Okay, (await driver.GetUserAsync(user.Id)).Status);

			MockSmsService.SentMessage standDown = Assert.Single(driver.SmsSince(before));
			Assert.Equal(contact.Phone, standDown.ToPhone);
			Assert.EndsWith(user.Name + " has indicated that they are no longer in an emergency status.", standDown.Body);
			Assert.Equal(SmsMessageTypeEnum.EmergencyCancelled, await LatestSmsTypeAsync(user.Id));

			Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsync($"api/users/{user.Id}/emergency/cancel", null)).StatusCode);
		}

		private Task<bool> HasLocationAsync(Guid userId)
		{
			return factory.InNewScopeAsync(db => db.StatusLogs.AnyAsync(l => l.UserId == userId && l.Location != null));
		}

		private Task<SmsMessageTypeEnum> LatestSmsTypeAsync(Guid userId)
		{
			return factory.InNewScopeAsync(db => db.Sms.Where(m => m.EmergencyContact.UserId == userId)
				.OrderByDescending(m => m.Id).Select(m => (SmsMessageTypeEnum)m.MessageTypeId).FirstAsync());
		}

		private static StatusLog NewLocationLog(Guid userId, DateTimeOffset at)
		{
			return new StatusLog
			{
				UserId = userId,
				TimeOccurrence = at,
				StatusId = (byte)StatusType.Pending1,
				Location = new NetTopologySuite.Geometries.Point(-78.2672, 0.3392) { SRID = 4326 },
				Notes = "Device location captured."
			};
		}
	}
}
