using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Xunit.Abstractions;

namespace AmigoAlert.Tests.Accuracy
{
	/// <summary>
	/// NF-01 and NF-02 accuracy evidence: forty check-in scenarios run through the real <see cref="EscalationService"/> against a real
	/// SQL Server database, with <see cref="MockSmsService"/> in place of Twilio. Every run writes one timestamped CSV.
	/// </summary>
	/// <remarks>
	/// Requires the <c>AMIGOALERT_TEST_SQL</c> environment variable to point at a migrated test database. Time is controlled by passing the
	/// evaluation instant to <see cref="EscalationService.EvaluateDueUsersAsync"/> and by the <see cref="TimeProvider"/> the service already
	/// takes. Every offset below is from S, the scheduled check-in instant; T is the escalation threshold, N plus both windows.
	/// </remarks>
	public sealed class AccuracyTests(ITestOutputHelper output)
	{
		private const string ConnectionVariable = "AMIGOALERT_TEST_SQL";

		/// <summary>The email domain of the users Seed.sql creates; nothing else is ever acted on.</summary>
		private const string SeedEmailDomain = "@amigoalert.test";

		private static readonly TimeSpan Zero = TimeSpan.Zero;

		/// <summary>N to the reminder.</summary>
		private static readonly TimeSpan Reminder = TimeSpan.FromMinutes(EscalationRules.ReminderAfterMinutes);

		/// <summary>The reminder to escalation, measured from when the reminder was actually sent.</summary>
		private static readonly TimeSpan Grace = TimeSpan.FromMinutes(EscalationRules.GraceMinutes);

		/// <summary>N to the escalation threshold: the Pending1 window followed by the Pending2 window.</summary>
		private static readonly TimeSpan T = TimeSpan.FromMinutes(EscalationRules.ReminderAfterMinutes + EscalationRules.GraceMinutes);

		private static readonly TimeOnly Morning = new TimeOnly(9, 0);

		private static readonly TimeOnly Evening = new TimeOnly(19, 0);

		/// <summary>Shared by both scored Facts, so one run produces one file.</summary>
		private static readonly string CsvPath = Path.Combine(AppContext.BaseDirectory,
			$"nf-accuracy-{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv");

		private static readonly Scenario[] Nf01 =
		[
			new("E-01", "No response at all, user 1", [new(0, StatusType.Escalated, [])], Cycle(Zero), [T + M(1)], Sms: 1),
			new("E-02", "No response at all, user 2", [new(1, StatusType.Escalated, [])], Cycle(Zero), [T + M(1)], Sms: 1),
			new("E-03", "No response at all, user 3", [new(2, StatusType.Escalated, [])], Cycle(Zero), [T + M(1)], Sms: 1),
			new("E-04", "No response at all, evening schedule", [new(3, StatusType.Escalated, [])], Cycle(Zero), [T + M(1)], Sms: 1,
				Schedule: Evening),
			new("E-05", "Notification sent 10 min late, no response", [new(4, StatusType.Escalated, [])], Cycle(M(10)),
				[M(10) + T + M(1)], Sms: 1),
			new("E-06", "Evaluated exactly at threshold", [new(5, StatusType.Escalated, [])], Cycle(Zero), [T], Sms: 1),
			new("E-07", "Notification on time, poll runs 5 min late", [new(0, StatusType.Escalated, [])], Cycle(Zero), [T + M(5)], Sms: 1),
			new("E-08", "Notification on time, poll runs 30 min late", [new(1, StatusType.Escalated, [])], Cycle(Zero), [T + M(30)],
				Sms: 1),
			new("E-09", "Notification on time, poll runs 2 hours late", [new(2, StatusType.Escalated, [])], Cycle(Zero), [T + H(2)],
				Sms: 1),
			// Escalated at T; the late check-in then returns the user to Okay, without retracting the alert already sent.
			new("E-10", "Check-in arrives after threshold", [new(3, StatusType.Okay, [T + M(2)], Alerted: true)], Cycle(Zero),
				[T, T + M(5)], Sms: 1),
			new("E-11", "Check-in arrives well after threshold", [new(4, StatusType.Okay, [T + M(20)], Alerted: true)], Cycle(Zero),
				[T, T + M(25)], Sms: 1),
			// The reminder poll never ran, so the user is still Pending1 when the threshold passes. The next poll sends the reminder late,
			// and escalation follows a full Pending2 window after it.
			new("E-12", "Still Pending1 at threshold", [new(5, StatusType.Escalated, [])], [Zero], [T + M(1), T + M(1) + Grace], Sms: 1),
			new("E-13", "Stale check-in from the previous cycle", [new(0, StatusType.Escalated, [-H(24)])], [-H(24), .. Cycle(Zero)],
				[T + M(1)], Sms: 1),
			new("E-14", "A different user checked in, this one did not",
				[new(1, StatusType.Escalated, []), new(2, StatusType.Okay, [M(5)])], Cycle(Zero), [T + M(1)], Sms: 1),
			new("E-15", "Two users both overdue", [new(3, StatusType.Escalated, []), new(4, StatusType.Escalated, [])], Cycle(Zero),
				[T + M(1)], Sms: 2),
			new("E-16", "Already escalated, poll runs again", [new(5, StatusType.Escalated, [])], [.. Cycle(Zero), T], [T + M(10)],
				Sms: 0),
			new("E-17", "Already escalated, poll runs much later", [new(0, StatusType.Escalated, [])], [.. Cycle(Zero), T], [T + H(2)],
				Sms: 0),
			new("E-18", "Emergency button pressed", [new(1, StatusType.Emergency, [])], [], [], Sms: 1, Emergency: Zero),
			new("E-19", "Emergency button pressed while Pending1", [new(2, StatusType.Emergency, [])], [Zero], [], Sms: 1,
				Emergency: M(1)),
			new("E-20", "Emergency button pressed, second user", [new(3, StatusType.Emergency, [])], [], [], Sms: 1, Emergency: Zero)
		];

		private static readonly Scenario[] Nf02 =
		[
			new("S-01", "Check-in shortly after the first notification, user 1", [new(0, StatusType.Okay, [M(5)])], Cycle(Zero),
				[T + M(1)], Sms: 0),
			new("S-02", "Check-in shortly after the first notification, user 2", [new(1, StatusType.Okay, [M(5)])], Cycle(Zero),
				[T + M(1)], Sms: 0),
			new("S-03", "Check-in shortly after the first notification, user 3", [new(2, StatusType.Okay, [M(5)])], Cycle(Zero),
				[T + M(1)], Sms: 0),
			new("S-04", "Check-in on the second notification", [new(3, StatusType.Okay, [Reminder + M(5)])], Cycle(Zero), [T + M(1)],
				Sms: 0),
			new("S-05", "Check-in on the second notification, second user", [new(4, StatusType.Okay, [Reminder + M(5)])], Cycle(Zero),
				[T + M(1)], Sms: 0),
			new("S-06", "Check-in 10 minutes before threshold", [new(5, StatusType.Okay, [T - M(10)])], Cycle(Zero), [T + M(1)], Sms: 0),
			new("S-07", "Check-in 1 minute before threshold", [new(0, StatusType.Okay, [T - M(1)])], Cycle(Zero), [T + M(1)], Sms: 0),
			new("S-08", "Check-in exactly at the scheduled instant", [new(1, StatusType.Okay, [Zero])], Cycle(Zero), [T + M(1)], Sms: 0),
			// Replaces "check-in before the scheduled instant": there is no notification to answer before S.
			new("S-09", "Check-in seconds after the notification arrives", [new(2, StatusType.Okay, [Sec(30)])], Cycle(Zero), [T + M(1)],
				Sms: 0),
			new("S-10", "Poll runs 30 minutes late, check-in was in time", [new(3, StatusType.Okay, [M(5)])], Cycle(Zero), [T + M(30)],
				Sms: 0),
			new("S-11", "Poll runs 2 hours late, check-in was in time", [new(4, StatusType.Okay, [M(5)])], Cycle(Zero), [T + H(2)],
				Sms: 0),
			new("S-12", "Check-in while status is Pending1", [new(5, StatusType.Okay, [Reminder - M(1)])], Cycle(Zero), [T + M(1)],
				Sms: 0),
			new("S-13", "Check-in while status is Pending2", [new(0, StatusType.Okay, [Reminder + M(1)])], Cycle(Zero), [T + M(1)],
				Sms: 0),
			new("S-14", "Duplicate check-in submitted twice", [new(1, StatusType.Okay, [M(5), M(5) + Sec(5)])], Cycle(Zero), [T + M(1)],
				Sms: 0),
			new("S-15", "Three check-ins in rapid succession", [new(2, StatusType.Okay, [M(5), M(5) + Sec(3), M(5) + Sec(6)])],
				Cycle(Zero), [T + M(1)], Sms: 0),
			new("S-16", "Two users both check in", [new(3, StatusType.Okay, [M(5)]), new(4, StatusType.Okay, [M(5)])], Cycle(Zero),
				[T + M(1)], Sms: 0),
			// The previous cycle was answered and the next one is three hours away.
			new("S-17", "Poll runs with nothing due", [new(5, StatusType.Okay, [-H(24) + M(5)])], Cycle(-H(24)), [-H(3)], Sms: 0,
				Unchanged: true),
			new("S-18", "Poll runs twice in the same minute", [new(0, StatusType.Okay, [M(5)])], Cycle(Zero), [T + M(1), T + M(1) + Sec(30)],
				Sms: 0),
			new("S-19", "Three consecutive cycles, all checked in", [new(1, StatusType.Okay, [M(5), H(24) + M(5), H(48) + M(5)])],
				[.. Cycle(Zero), .. Cycle(H(24)), .. Cycle(H(48))], [T + M(1), H(24) + T + M(1), H(48) + T + M(1)], Sms: 0,
				EachPoll: true),
			new("S-20", "Check-in near the end of the window", [new(2, StatusType.Okay, [T - M(2)])], Cycle(Zero), [T + M(1)], Sms: 0)
		];

		/// <summary>
		/// Unscored: the notification went out 10 minutes late, and the poll runs at the instant that would have been the threshold had it
		/// been on time. The user's window is not over, so nothing may be escalated yet.
		/// </summary>
		private static readonly Scenario EarlyEscalationGuard = new("G-01", "Notification 10 min late, polled at the on-time threshold",
			[new(0, StatusType.Pending2, [])], Cycle(M(10)), [T + M(1)], Sms: 0);

		private enum Step
		{
			// Declared in the order steps run when they share an instant: a notification must exist before it can be answered.
			SetupPoll,
			Poll,
			Emergency,
			CheckIn
		}

		[Fact]
		public Task Nf01_MissedCheckInsEscalateToEmergencyContact()
		{
			return RunSuiteAsync("NF-01", Nf01);
		}

		[Fact]
		public Task Nf02_SuccessfulCheckInsEndAtOkay()
		{
			return RunSuiteAsync("NF-02", Nf02);
		}

		[Fact]
		public async Task LateNotificationIsNotEscalatedEarly()
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			Clock clock = new Clock();
			await using ServiceProvider provider = BuildServices(clock);
			User[] users = await SeedAsync(provider);

			Result result = await RunAsync(provider, clock, users, LocalDate(now), "unscored", EarlyEscalationGuard);

			Assert.True(result.Passed, result.Failure);
		}

		private async Task RunSuiteAsync(string requirement, Scenario[] scenarios)
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			Clock clock = new Clock();
			await using ServiceProvider provider = BuildServices(clock);
			User[] users = await SeedAsync(provider);

			List<string> rows = [];
			List<string> failures = [];
			foreach (Scenario scenario in scenarios)
			{
				Result result = await RunAsync(provider, clock, users, LocalDate(now), requirement, scenario);
				rows.Add(result.CsvRow);
				if (!result.Passed)
				{
					failures.Add(result.Failure);
				}
			}

			if (!File.Exists(CsvPath))
			{
				await File.WriteAllTextAsync(CsvPath, "ScenarioId,Requirement,Description,ScheduledForUtc,CheckInRecordedUtc,EvaluatedUtc," +
					"FinalStatus,SmsCount,SmsRecipient,SmsSentUtc,Expected,Result" + Environment.NewLine);
			}

			await File.AppendAllLinesAsync(CsvPath, rows);

			int passed = scenarios.Length - failures.Count;
			output.WriteLine($"{requirement}: {passed}/{scenarios.Length} passed. Evidence: {CsvPath}");
			if (requirement == "NF-01")
			{
				output.WriteLine("E-18 to E-20 call EscalationService.ActivateEmergencyAsync, the emergency button's entry point; the poll " +
					"loop does not handle the button.");
			}

			Assert.True(passed / (double)scenarios.Length >= 0.95,
				$"{requirement} accuracy {passed}/{scenarios.Length}. Failures: {string.Join("; ", failures)}");
		}

		/// <summary>
		/// Resets the scenario's users, then runs its setup polls, check-ins, scored polls and emergency press in time order. The SMS record
		/// is cleared immediately before the first scored call. Each user must end at the expected status after the last scored call, or after
		/// every one for <see cref="Scenario.EachPoll"/>.
		/// </summary>
		private static async Task<Result> RunAsync(ServiceProvider provider, Clock clock, User[] users, DateOnly today, string requirement,
			Scenario scenario)
		{
			using IServiceScope scope = provider.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();
			EscalationService service = scope.ServiceProvider.GetRequiredService<EscalationService>();
			MockSmsService sms = (MockSmsService)scope.ServiceProvider.GetRequiredService<ISmsService>();

			TimeOnly schedule = scenario.Schedule ?? Morning;
			DateTimeOffset s = new DateTimeOffset(today.ToDateTime(schedule), EscalationRules.LocalOffset);
			Guid[] ids = scenario.Users.Select(p => users[p.User].Id).ToArray();
			StatusType[] expected = scenario.Users.Select(p => p.Expected).ToArray();

			await ResetAsync(db, users, ids, schedule);

			List<(TimeSpan At, Step Step, int User)> steps = [];
			steps.AddRange(scenario.Setup.Select(at => (at, Step.SetupPoll, -1)));
			steps.AddRange(scenario.Polls.Select(at => (at, Step.Poll, -1)));
			steps.AddRange(scenario.Users.SelectMany(p => p.CheckIns.Select(at => (at, Step.CheckIn, p.User))));
			if (scenario.Emergency is TimeSpan pressedAt)
			{
				steps.Add((pressedAt, Step.Emergency, scenario.Users[0].User));
			}

			bool cleared = false;
			bool statusesMatched = true;
			StatusType[] before = [];
			StatusType[] final = [];
			DateTimeOffset evaluated = s;

			foreach ((TimeSpan at, Step step, int user) in steps.OrderBy(x => x.At).ThenBy(x => x.Step))
			{
				clock.Now = s + at;
				bool scored = step is Step.Poll or Step.Emergency;

				if (scored && !cleared)
				{
					before = await StatusesAsync(db, ids);
					sms.Clear();
					cleared = true;
				}

				switch (step)
				{
					case Step.SetupPoll:
					case Step.Poll:
						await PollAsync(service, clock.Now, ids);
						break;
					case Step.CheckIn:
						await CheckInAsync(db, service, users[user].Id);
						break;
					case Step.Emergency:
						await service.ActivateEmergencyAsync(users[user].Id, null, null);
						break;
				}

				if (scored)
				{
					evaluated = clock.Now;
					final = await StatusesAsync(db, ids);
					statusesMatched &= !scenario.EachPoll || final.SequenceEqual(expected);
				}
			}

			string[] expectedTo = scenario.Sms == 0 ? [] : scenario.Users
				.Where(p => p.Alerted || p.Expected is StatusType.Escalated or StatusType.Emergency)
				.Select(p => ContactPhone(users[p.User]))
				.Order()
				.ToArray();
			string[] sentTo = sms.Sent.Select(m => m.ToPhone).Order().ToArray();

			bool passed = statusesMatched && final.SequenceEqual(expected) && sentTo.Length == scenario.Sms && sentTo.SequenceEqual(expectedTo)
				&& (!scenario.Unchanged || before.SequenceEqual(final));

			List<DateTimeOffset> received = await db.CheckIns
				.Where(c => ids.Contains(c.UserId) && c.TimeReceived != null)
				.Select(c => c.TimeReceived!.Value)
				.ToListAsync();

			string csvRow = string.Join(",", Csv(scenario.Id), Csv(requirement), Csv(scenario.Description), Iso(s),
				Csv(string.Join(";", received.Distinct().Order().Select(Iso))), Iso(evaluated), Csv(string.Join(";", final)),
				sentTo.Length.ToString(CultureInfo.InvariantCulture), Csv(string.Join(";", sms.Sent.Select(m => m.ToPhone))),
				Csv(string.Join(";", sms.Sent.Select(m => Iso(m.SentAt)))), Csv(string.Join(";", expected)), passed ? "PASS" : "FAIL");

			string failure = $"{scenario.Id} expected {string.Join(";", expected)} with {scenario.Sms} SMS, got {string.Join(";", final)} " +
				$"with {sentTo.Length} SMS{(sentTo.Length > 0 ? " to " + string.Join(";", sentTo) : "")}";

			return new Result(passed, csvRow, failure);
		}

		/// <summary>One WebJob run, as AmigoAlert.WebJob performs it, restricted to the scenario's users.</summary>
		private static async Task PollAsync(EscalationService service, DateTimeOffset now, Guid[] ids)
		{
			IReadOnlyList<EscalationDecision> decisions = await service.EvaluateDueUsersAsync(now);
			foreach (EscalationDecision decision in decisions)
			{
				if (decision.IsActionable && ids.Contains(decision.UserId))
				{
					await service.ActOnDecisionAsync(decision);
				}
			}
		}

		/// <summary>The user taps "I'm okay" on the newest notification they were sent, as the device would.</summary>
		private static async Task CheckInAsync(AmigoAlertDbContext db, EscalationService service, Guid userId)
		{
			long checkInId = await db.CheckIns
				.Where(c => c.UserId == userId)
				.OrderByDescending(c => c.ScheduledFor)
				.ThenByDescending(c => c.Id)
				.Select(c => c.Id)
				.FirstAsync();

			await service.RecordCheckInResponseAsync(checkInId, CheckInResponseType.Okay);
		}

		/// <summary>Clears every seeded user's history back to Okay with no schedule, then gives the scenario's users theirs.</summary>
		private static async Task ResetAsync(AmigoAlertDbContext db, User[] users, Guid[] ids, TimeOnly schedule)
		{
			Guid[] seeded = users.Select(u => u.Id).ToArray();

			await db.Sms.Where(m => seeded.Contains(m.EmergencyContact.UserId)).ExecuteDeleteAsync();
			await db.StatusLogs.Where(l => seeded.Contains(l.UserId)).ExecuteDeleteAsync();
			await db.CheckIns.Where(c => seeded.Contains(c.UserId)).ExecuteDeleteAsync();
			await db.Users.Where(u => seeded.Contains(u.Id)).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.StatusId, (byte)StatusType.Okay)
				.SetProperty(u => u.Notification1Time, (TimeOnly?)null)
				.SetProperty(u => u.Notification2Time, (TimeOnly?)null));
			await db.Users.Where(u => ids.Contains(u.Id)).ExecuteUpdateAsync(x => x.SetProperty(u => u.Notification1Time, schedule));
		}

		private static async Task<StatusType[]> StatusesAsync(AmigoAlertDbContext db, Guid[] ids)
		{
			StatusType[] statuses = new StatusType[ids.Length];
			for (int i = 0; i < ids.Length; i++)
			{
				Guid id = ids[i];
				statuses[i] = (StatusType)await db.Users.Where(u => u.Id == id).Select(u => u.StatusId).SingleAsync();
			}

			return statuses;
		}

		private static async Task<User[]> SeedAsync(ServiceProvider provider)
		{
			using IServiceScope scope = provider.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Accuracy", "Seed.sql"));
			await db.Database.ExecuteSqlRawAsync(sql);

			return await db.Users
				.Include(u => u.EmergencyContacts)
				.Where(u => u.Email.EndsWith(SeedEmailDomain))
				.OrderBy(u => u.Email)
				.ToArrayAsync();
		}

		/// <summary>The data layer and the real escalation service, with the mock SMS service and a no-op push. No Twilio client exists.</summary>
		private static ServiceProvider BuildServices(Clock clock)
		{
			string? connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new InvalidOperationException($"Set {ConnectionVariable} to the connection string of a migrated test database " +
					"(LocalDB or a dedicated Azure SQL test database, never the real one).");
			}

			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddAmigoAlertData(connectionString);
			services.AddSingleton<TimeProvider>(clock);
			services.AddSingleton<IPushNotificationService, NoPush>();
			services.AddScoped<ISmsService, MockSmsService>();
			services.AddScoped<EscalationService>();

			return services.BuildServiceProvider();
		}

		private static DateOnly LocalDate(DateTimeOffset now)
		{
			return DateOnly.FromDateTime(now.ToOffset(EscalationRules.LocalOffset).DateTime);
		}

		private static string ContactPhone(User user)
		{
			return user.EmergencyContacts.Single().Phone;
		}

		/// <summary>The notification poll at N and the reminder poll one window later.</summary>
		private static TimeSpan[] Cycle(TimeSpan notifiedAt)
		{
			return [notifiedAt, notifiedAt + Reminder];
		}

		private static TimeSpan Sec(int seconds)
		{
			return TimeSpan.FromSeconds(seconds);
		}

		private static TimeSpan M(int minutes)
		{
			return TimeSpan.FromMinutes(minutes);
		}

		private static TimeSpan H(int hours)
		{
			return TimeSpan.FromHours(hours);
		}

		private static string Iso(DateTimeOffset instant)
		{
			return instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
		}

		private static string Csv(string value)
		{
			return value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
		}

		/// <summary>One user in a scenario: which seeded user, when they tap "I'm okay" (offsets from S), and where they must end.</summary>
		/// <param name="User">The index of the seeded user.</param>
		/// <param name="Expected">The status the user must end at.</param>
		/// <param name="CheckIns">When the user taps "I'm okay", as offsets from S.</param>
		/// <param name="Alerted">Whether the contact must be messaged although the user ends at neither Escalated nor Emergency.</param>
		private sealed record Participant(int User, StatusType Expected, TimeSpan[] CheckIns, bool Alerted = false);

		/// <summary>
		/// One scenario. <paramref name="Setup"/> polls build the starting state; <paramref name="Polls"/> and <paramref name="Emergency"/>
		/// are the scored calls. All offsets are from S.
		/// </summary>
		/// <param name="Id">The scenario id from the brief.</param>
		/// <param name="Description">What the scenario covers.</param>
		/// <param name="Users">The users involved and their expected final status.</param>
		/// <param name="Setup">Polls run before the SMS record is cleared.</param>
		/// <param name="Polls">Scored polls; the last is the evaluation instant.</param>
		/// <param name="Sms">The number of SMS the scored calls must send, each to the contact of an escalated, emergency or alerted user.</param>
		/// <param name="Emergency">When the first user presses the emergency button, if they do.</param>
		/// <param name="Schedule">The user's local check-in time; morning unless given.</param>
		/// <param name="Unchanged">Whether the status must be the same after the scored calls as before them.</param>
		/// <param name="EachPoll">Whether every scored call, not only the last, must leave each user at the expected status.</param>
		private sealed record Scenario(string Id, string Description, Participant[] Users, TimeSpan[] Setup, TimeSpan[] Polls, int Sms,
			TimeSpan? Emergency = null, TimeOnly? Schedule = null, bool Unchanged = false, bool EachPoll = false);

		private sealed record Result(bool Passed, string CsvRow, string Failure);

		/// <summary>The clock the service reads; set to each step's instant before the step runs.</summary>
		private sealed class Clock : TimeProvider
		{
			public DateTimeOffset Now { get; set; }

			public override DateTimeOffset GetUtcNow()
			{
				return Now;
			}
		}

		/// <summary>Push is best effort and escalation does not depend on it, so nothing is pushed.</summary>
		private sealed class NoPush : IPushNotificationService
		{
			public Task<bool> RegisterDeviceAsync(Guid userId, Guid installationId, string pushChannel, CancellationToken ct = default)
			{
				return Task.FromResult(true);
			}

			public Task<bool> SendCheckInAsync(Guid userId, long checkInId, bool isReminder, DateTimeOffset sentAt,
				CancellationToken ct = default)
			{
				return Task.FromResult(true);
			}
		}
	}
}
