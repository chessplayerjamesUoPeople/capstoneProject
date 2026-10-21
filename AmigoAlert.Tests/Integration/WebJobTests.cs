using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-14 and I-15: one WebJob run through its own entry point, <c>Program.RunAsync</c>, with the container composed as
	/// <c>Program.Main</c> composes it. The accuracy suite drives <see cref="EscalationService"/> directly; these prove the WebJob reaches
	/// that same service rather than carrying logic of its own.
	/// </summary>
	/// <remarks>
	/// The run acts on every user in the database, as the real one does, so these assume no user outside Seed.sql has a check-in schedule.
	/// </remarks>
	[Collection(IntegrationCollection.Name)]
	public sealed class WebJobTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
		/// <summary>Seed.sql's users, each with a contact who has granted consent and, until a test sets one, no schedule.</summary>
		private static readonly Guid[] SeededUserIds = Enumerable.Range(1, 6)
			.Select(i => Guid.Parse($"a1000000-0000-0000-0000-00000000000{i}"))
			.ToArray();

		private static readonly TimeOnly Schedule = new TimeOnly(9, 0);

		/// <summary>The occurrence under test. Any past date works: the run evaluates at the instant it is given.</summary>
		private static readonly DateTimeOffset S = new DateTimeOffset(2026, 10, 1, 9, 0, 0, EscalationRules.LocalOffset);

		public Task InitializeAsync()
		{
			return factory.ResetAsync();
		}

		public Task DisposeAsync()
		{
			return factory.RemoveRegisteredUsersAsync();
		}

		[Fact]
		public async Task I14_PollWithOverdueUser_EscalatesAndTextsTheContact()
		{
			Guid userId = SeededUserIds[0];

			// Both notifications sent and unanswered, the reminder's window over: overdue. TimeSent is the second before the sending run's
			// minute, as EscalationService records it.
			await ScheduleAsync(userId, StatusType.Pending2,
				NewCheckIn(userId, S.AddSeconds(-1), received: null),
				NewCheckIn(userId, S.AddMinutes(EscalationRules.ReminderAfterMinutes).AddSeconds(-1), received: null));

			await RunWebJobAsync(S.AddMinutes(EscalationRules.ReminderAfterMinutes + EscalationRules.GraceMinutes + 1));

			User user = await factory.InNewScopeAsync(db => db.Users.Include(u => u.EmergencyContacts).SingleAsync(u => u.Id == userId));
			EmergencyContact contact = Assert.Single(user.EmergencyContacts);
			Assert.Equal((byte)StatusType.Escalated, user.StatusId);

			StatusLog log = await factory.InNewScopeAsync(db => db.StatusLogs.SingleAsync(l => l.UserId == userId));
			Assert.Equal((byte)StatusType.Escalated, log.StatusId);

			Sms sms = await factory.InNewScopeAsync(db => db.Sms.SingleAsync(m => m.EmergencyContactId == contact.Id));
			Assert.Equal((byte)SmsMessageTypeEnum.MissedCheckInEscalation, sms.MessageTypeId);
			Assert.Equal(log.Id, sms.StatusLogId);

			MockSmsService.SentMessage sent = Assert.Single(factory.SentSms);
			Assert.Equal(contact.Phone, sent.ToPhone);
		}

		[Fact]
		public async Task I15_PollWithNothingDue_ChangesNothing()
		{
			Guid userId = SeededUserIds[1];

			// Today's occurrence was answered, and the next is a day away.
			await ScheduleAsync(userId, StatusType.Okay, NewCheckIn(userId, S.AddSeconds(-1), received: S.AddMinutes(5)));
			int checkInsBefore = await factory.InNewScopeAsync(db => db.CheckIns.CountAsync(c => SeededUserIds.Contains(c.UserId)));

			await RunWebJobAsync(S.AddHours(2));

			Assert.All(await factory.InNewScopeAsync(db => db.Users.Where(u => SeededUserIds.Contains(u.Id)).Select(u => u.StatusId)
				.ToListAsync()), status => Assert.Equal((byte)StatusType.Okay, status));
			Assert.Equal(checkInsBefore, await factory.InNewScopeAsync(db => db.CheckIns.CountAsync(c => SeededUserIds.Contains(c.UserId))));
			Assert.False(await factory.InNewScopeAsync(db => db.StatusLogs.AnyAsync(l => SeededUserIds.Contains(l.UserId))));
			Assert.False(await factory.InNewScopeAsync(db => db.Sms.AnyAsync(m => SeededUserIds.Contains(m.EmergencyContact.UserId))));
			Assert.Empty(factory.SentSms);
		}

		/// <summary>
		/// One WebJob run at <paramref name="now"/>, with the registrations <c>Program.Main</c> makes and the mock switched on the same way.
		/// </summary>
		private async Task RunWebJobAsync(DateTimeOffset now)
		{
			IConfiguration configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["Twilio:UseMockSms"] = "true" })
				.Build();

			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddAmigoAlertData(factory.ConnectionString);
			services.AddAmigoAlertServices(configuration);
			factory.RecordSms(services);

			await using ServiceProvider provider = services.BuildServiceProvider();
			await AmigoAlert.WebJob.Program.RunAsync(provider, now);
		}

		private Task<int> ScheduleAsync(Guid userId, StatusType status, params CheckIn[] checkIns)
		{
			return factory.InNewScopeAsync(async db =>
			{
				db.CheckIns.AddRange(checkIns);
				await db.SaveChangesAsync();

				return await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
					.SetProperty(u => u.Notification1Time, Schedule)
					.SetProperty(u => u.StatusId, (byte)status));
			});
		}

		private static CheckIn NewCheckIn(Guid userId, DateTimeOffset sentAt, DateTimeOffset? received)
		{
			return new CheckIn
			{
				UserId = userId,
				ScheduledFor = S,
				TimeSent = sentAt,
				TimeReceived = received,
				ResponseId = (byte)(received is null ? CheckInResponseType.NoResponse : CheckInResponseType.Okay)
			};
		}
	}
}
