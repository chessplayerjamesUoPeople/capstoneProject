using AmigoAlert.Data;
using AmigoAlert.Services;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Tests.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace AmigoAlert.Tests.SystemTests
{
	/// <summary>
	/// Drives the whole system the way its users do: the device through the API, the contact through the consent link in the SMS, and the
	/// WebJob through its own entry point at chosen instants. Nothing is set up by writing to the database, so each journey proves the
	/// components work together rather than one at a time.
	/// </summary>
	/// <remarks>
	/// The WebJob reads its clock from <see cref="TimeProvider"/>, so the journeys run it at the occurrence and at each wait after it
	/// without waiting in real time. The occurrence is half an hour in the past, so anything the API records by the real clock (a
	/// location, an answer) falls after it, as it would on a device.
	/// </remarks>
	internal sealed class SystemTestDriver(IntegrationApiFactory factory)
	{
		private static readonly Regex ConsentToken = new Regex("[0-9a-f]{32}", RegexOptions.Compiled);

		public HttpClient Client { get; } = factory.CreateClient();

		/// <summary>The occurrence the journeys schedule their user for: a whole minute, half an hour ago.</summary>
		public DateTimeOffset Occurrence { get; } = FloorToMinute(DateTimeOffset.UtcNow).AddMinutes(-30);

		/// <summary>The first run after the first notification at which the reminder is due.</summary>
		public DateTimeOffset ReminderDue => Occurrence.AddMinutes(EscalationRules.ReminderAfterMinutes);

		/// <summary>The first run after the reminder at which escalation is due.</summary>
		public DateTimeOffset EscalationDue => ReminderDue.AddMinutes(EscalationRules.GraceMinutes);

		/// <summary>
		/// A registration whose first check-in is <see cref="Occurrence"/> and whose second is twelve hours away, so only one occurrence
		/// is in play during a journey.
		/// </summary>
		public CreateUserDto NewUser()
		{
			TimeOnly first = TimeOnly.FromDateTime(Occurrence.ToOffset(EscalationRules.LocalOffset).DateTime);

			return RegistrationTests.NewUser() with { Notification1Time = first, Notification2Time = first.AddHours(12) };
		}

		public Task<UserDto> RegisterAsync(CreateUserDto? request = null)
		{
			return RegistrationTests.RegisterAsync(Client, request ?? NewUser());
		}

		/// <summary>Adds the contact and returns the consent token read from the link in the SMS it was sent.</summary>
		public async Task<Guid> AddContactAsync(Guid userId, SetEmergencyContactDto contact)
		{
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			HttpResponseMessage response = await Client.PostAsJsonAsync($"api/users/{userId}/emergency-contact", contact);
			Assert.Equal(HttpStatusCode.Created, response.StatusCode);

			return TokenFromLatestSms(before, contact.Phone);
		}

		/// <summary>Replaces the contact and returns the consent token from the new request.</summary>
		public async Task<Guid> ReplaceContactAsync(Guid userId, SetEmergencyContactDto contact)
		{
			IReadOnlyList<MockSmsService.SentMessage> before = factory.SentSms;

			HttpResponseMessage response = await Client.PutAsJsonAsync($"api/users/{userId}/emergency-contact", contact);
			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

			return TokenFromLatestSms(before, contact.Phone);
		}

		public Task<HttpResponseMessage> AnswerConsentAsync(Guid token, bool granted)
		{
			return Client.PutAsJsonAsync($"api/consent/{token}", new RespondToConsentDto(granted));
		}

		/// <summary>Registration, a contact, and the contact's acceptance: the state in which the app is fully usable.</summary>
		public async Task<(UserDto User, SetEmergencyContactDto Contact)> ConsentedUserAsync(CreateUserDto? request = null)
		{
			UserDto user = await RegisterAsync(request);
			SetEmergencyContactDto contact = new SetEmergencyContactDto("System Contact", "+593990007777");
			Guid token = await AddContactAsync(user.Id, contact);
			Assert.Equal(HttpStatusCode.NoContent, (await AnswerConsentAsync(token, granted: true)).StatusCode);

			return (user, contact);
		}

		public async Task<UserDto> GetUserAsync(Guid userId)
		{
			return (await Client.GetFromJsonAsync<UserDto>($"api/users/{userId}"))!;
		}

		public async Task<EmergencyContactDto> GetContactAsync(Guid userId)
		{
			return (await Client.GetFromJsonAsync<EmergencyContactDto>($"api/users/{userId}/emergency-contact"))!;
		}

		public async Task<IReadOnlyList<CheckInDto>> GetCheckInsAsync(Guid userId)
		{
			return (await Client.GetFromJsonAsync<List<CheckInDto>>($"api/users/{userId}/checkins?since={Uri.EscapeDataString(
				Occurrence.AddMinutes(-1).ToString("O"))}"))!;
		}

		/// <summary>
		/// The SMS sent since <paramref name="before"/> was taken from <see cref="IntegrationApiFactory.SentSms"/>. By identity, not by
		/// position: the WebJob's messages carry its simulated clock, so they sort ahead of messages the API sent in real time.
		/// </summary>
		public IReadOnlyList<MockSmsService.SentMessage> SmsSince(IReadOnlyList<MockSmsService.SentMessage> before)
		{
			return factory.SentSms.Where(m => !before.Contains(m, ReferenceEqualityComparer.Instance)).ToList();
		}

		/// <summary>
		/// One WebJob run at <paramref name="now"/>, composed as <c>Program.Main</c> composes it, with the mock switched on through
		/// configuration and the clock set to <paramref name="now"/>.
		/// </summary>
		public async Task RunWebJobAsync(DateTimeOffset now)
		{
			IConfiguration configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["Twilio:UseMockSms"] = "true" })
				.Build();

			ServiceCollection services = new ServiceCollection();
			services.AddLogging();

			// Registered first: AddAmigoAlertServices only adds the system clock when none is registered.
			services.AddSingleton<TimeProvider>(new FixedClock(now));
			services.AddAmigoAlertData(factory.ConnectionString);
			services.AddAmigoAlertServices(configuration);
			factory.RecordSms(services);

			await using ServiceProvider provider = services.BuildServiceProvider();
			await AmigoAlert.WebJob.Program.RunAsync(provider, now);
		}

		/// <summary>NF-13: every message fits one 160-character segment of plain ASCII, so the carrier never switches to UCS-2.</summary>
		public static void AssertSingleSegment(MockSmsService.SentMessage message)
		{
			Assert.InRange(message.Body.Length, 1, SmsMessageComposer.MaxLength);
			Assert.All(message.Body, c => Assert.InRange(c, ' ', '~'));
		}

		private Guid TokenFromLatestSms(IReadOnlyList<MockSmsService.SentMessage> before, string contactPhone)
		{
			MockSmsService.SentMessage request = Assert.Single(SmsSince(before));
			Assert.Equal(contactPhone, request.ToPhone);

			return Guid.ParseExact(ConsentToken.Match(request.Body).Value, "N");
		}

		private static DateTimeOffset FloorToMinute(DateTimeOffset value)
		{
			return new DateTimeOffset(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, value.Offset);
		}

		private sealed class FixedClock(DateTimeOffset now) : TimeProvider
		{
			public override DateTimeOffset GetUtcNow()
			{
				return now;
			}
		}
	}
}
