using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Twilio.Clients;
using Twilio.Exceptions;
using Xunit.Abstractions;

// AccuracyTests and LatencyTests both run Seed.sql and reset the same seeded users, so they must not run at the same time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AmigoAlert.Tests.Latency
{
	/// <summary>
	/// NF-03 latency evidence: at most 60 seconds from the escalation decision or the emergency button press to Twilio accepting the SMS.
	/// Four phases, run in order, each green before the next: the mock, a free rejection check against the real Twilio account, one live
	/// message, then the ten scored live runs. Every run writes one timestamped CSV, a row at a time.
	/// </summary>
	/// <remarks>
	/// T0 is <see cref="StatusLog.TimeOccurrence"/> of the Escalated or Emergency row, and T1 is <see cref="Sms.TimeSent"/> of the
	/// <c>Sms</c> row pointing at it, which <see cref="SmsService"/> stamps as soon as Twilio returns. Both come from the database, so the
	/// timestamps compared are the system's own, not the test's. The stopwatch is the authoritative number: it starts once the escalation
	/// decision is made (or as the endpoint is called) and stops when the call returns, so it also covers the commit after T1.
	/// <para>
	/// Requires <c>AMIGOALERT_TEST_SQL</c>, as AccuracyTests does. Phase 1 also needs <c>TWILIO_ACCOUNT_SID</c> and <c>TWILIO_AUTH_TOKEN</c>
	/// set to the live credentials; the account has no test credentials, so phase 1 proves the real path with requests Twilio is certain
	/// to refuse, which are neither sent nor billed. Phases 2 and 3 send real, billed SMS and run only with <c>AMIGOALERT_LIVE_SMS=1</c>, the live
	/// credentials, <c>TWILIO_MESSAGING_SERVICE_SID</c> and <c>AMIGOALERT_LIVE_SMS_TO</c>; every live message goes to that one number.
	/// </para>
	/// </remarks>
	public sealed class LatencyTests(ITestOutputHelper output)
	{
		private const string ConnectionVariable = "AMIGOALERT_TEST_SQL";

		private const string LiveVariable = "AMIGOALERT_LIVE_SMS";

		private const string RecipientVariable = "AMIGOALERT_LIVE_SMS_TO";

		private const string AccountSidVariable = "TWILIO_ACCOUNT_SID";

		private const string AuthTokenVariable = "TWILIO_AUTH_TOKEN";

		private const string MessagingServiceVariable = "TWILIO_MESSAGING_SERVICE_SID";

		/// <summary>The email domain of the users Seed.sql creates; nothing else is ever acted on.</summary>
		private const string SeedEmailDomain = "@amigoalert.test";

		/// <summary>
		/// Twilio's magic test sender. Phase 1 sends from it in place of the Messaging Service: under live credentials it is not a number the
		/// account owns, so Twilio refuses every request before anything is sent or billed.
		/// </summary>
		private const string TestFromNumber = "+15005550006";

		/// <summary>The most live messages one test run may send: 1 smoke test, 10 scored runs, 1 spare.</summary>
		private const int MaxLiveSends = 12;

		private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

		/// <summary>From the end of one live send to the start of the next, so Twilio does not treat the run as a burst.</summary>
		private static readonly TimeSpan LiveSpacing = TimeSpan.FromSeconds(60);

		/// <summary>The same gap in the free phases, kept short: it sits outside the timed window either way.</summary>
		private static readonly TimeSpan RehearsalSpacing = TimeSpan.FromSeconds(1);

		/// <summary>The most the stopwatch and the stored timestamps may disagree before the clock is suspect.</summary>
		private static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds(1);

		private static readonly TimeSpan TwilioTimeout = TimeSpan.FromSeconds(30);

		/// <summary>N to the escalation threshold: the Pending1 window followed by the Pending2 window.</summary>
		private static readonly TimeSpan T = TimeSpan.FromMinutes(EscalationRules.ReminderAfterMinutes + EscalationRules.GraceMinutes);

		private static readonly TimeSpan Reminder = TimeSpan.FromMinutes(EscalationRules.ReminderAfterMinutes);

		/// <summary>Twilio's statuses for a message it has accepted but not yet handed to the carrier.</summary>
		private static readonly string[] AcceptedStatuses = ["queued", "accepted"];

		/// <summary>Twilio's refusals of a sender the account does not own: 21606, and 21659 on newer accounts.</summary>
		private static readonly int[] SenderRefusals = [21606, 21659];

		private static readonly Regex E164 = new Regex(@"^\+[1-9]\d{7,14}$");

		/// <summary>Shared by every phase in the process, so one run produces one file.</summary>
		private static readonly string CsvPath = Path.Combine(AppContext.BaseDirectory,
			$"nf03-latency-{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv");

		/// <summary>L-04, L-05, L-09 and L-10 reuse a user, which proves the reset clears the terminal status in between.</summary>
		private static readonly Run[] Scored =
		[
			new("L-01", Trigger.EscalationLogic, 0),
			new("L-02", Trigger.EscalationLogic, 1),
			new("L-03", Trigger.EscalationLogic, 2),
			new("L-04", Trigger.EscalationLogic, 0),
			new("L-05", Trigger.EscalationLogic, 1),
			new("L-06", Trigger.EmergencyButton, 0),
			new("L-07", Trigger.EmergencyButton, 1),
			new("L-08", Trigger.EmergencyButton, 2),
			new("L-09", Trigger.EmergencyButton, 0),
			new("L-10", Trigger.EmergencyButton, 1)
		];

		/// <summary>One refused alert per trigger path.</summary>
		private static readonly Run[] Rejection = [new("R-01", Trigger.EscalationLogic, 0), new("R-02", Trigger.EmergencyButton, 1)];

		private static readonly Run[] Smoke = [new("SMOKE", Trigger.EscalationLogic, 0)];

		/// <summary>Live messages sent by this process, across every phase. Checked before each send, never after.</summary>
		private static int liveSends;

		private enum Trigger
		{
			EscalationLogic,
			EmergencyButton
		}

		private enum Phase
		{
			Mock,
			Rejection,
			Smoke,
			Live
		}

		[Fact]
		public Task Phase0_MockSms()
		{
			return RunPhaseAsync(Phase.Mock, Scored);
		}

		/// <summary>
		/// Free: the real SmsService, client, auth handshake and error handling against the live account, sending from a number Twilio
		/// refuses. Passes when both trigger paths are refused for the sender and the refused alert leaves no state behind.
		/// </summary>
		[Fact]
		[Trait("Category", "TwilioRejection")]
		public Task Phase1_LiveCredentialsRefuseTestSender()
		{
			if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AccountSidVariable))
				|| string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AuthTokenVariable)))
			{
				output.WriteLine($"Skipped: set {AccountSidVariable} and {AuthTokenVariable} to the live Twilio credentials.");
				return Task.CompletedTask;
			}

			return RunPhaseAsync(Phase.Rejection, Rejection);
		}

		[Fact]
		[Trait("Category", "LiveSms")]
		public Task Phase2_LiveSmokeTest()
		{
			if (!LiveEnabled())
			{
				return Task.CompletedTask;
			}

			return RunPhaseAsync(Phase.Smoke, Smoke);
		}

		[Fact]
		[Trait("Category", "LiveSms")]
		public Task Phase3_LiveScoredRuns()
		{
			if (!LiveEnabled())
			{
				return Task.CompletedTask;
			}

			return RunPhaseAsync(Phase.Live, Scored);
		}

		/// <summary>
		/// Seeds, then runs each iteration in turn: reset, trigger, measure, record. The first failure of any kind writes its row and aborts
		/// the rest, without retrying; a partial run is evidence, a broken one is spent money.
		/// </summary>
		private async Task RunPhaseAsync(Phase phase, Run[] runs)
		{
			bool live = phase is Phase.Smoke or Phase.Live;
			string connectionString = Require(ConnectionVariable, "the connection string of a migrated test database (LocalDB or a " +
				"dedicated Azure SQL test database, never the real one)");
			string? recipient = live ? Require(RecipientVariable, "the one phone number every live message may go to") : null;

			if (recipient is not null && !E164.IsMatch(recipient))
			{
				throw new InvalidOperationException($"{RecipientVariable} must be an E.164 number such as +593..., not '{recipient}'.");
			}

			if (live && liveSends + runs.Length > MaxLiveSends)
			{
				throw new InvalidOperationException($"{runs.Length} more live messages would exceed the cap of {MaxLiveSends}; " +
					$"{liveSends} already sent by this run.");
			}

			TwilioOptions twilio = TwilioSettings(phase);
			GuardedTwilioClient client = phase == Phase.Mock
				? new GuardedTwilioClient(null, null, null)
				: new GuardedTwilioClient(new TwilioRestClient(twilio.AccountSid, twilio.AuthToken),
					phase == Phase.Rejection ? TestFromNumber : null, recipient);

			void Override(IServiceCollection services)
			{
				services.Configure<TwilioOptions>(o =>
				{
					o.AccountSid = twilio.AccountSid;
					o.AuthToken = twilio.AuthToken;
					o.MessagingServiceSid = twilio.MessagingServiceSid;
					// Test messages never trigger callbacks, and live ones have nowhere to land from a test run.
					o.StatusCallbackUrl = string.Empty;
				});
				services.AddSingleton<ITwilioRestClient>(client);
				services.AddSingleton<IPushNotificationService, NoPush>();
				if (phase == Phase.Mock)
				{
					services.AddScoped<ISmsService, MockSmsService>();
				}
			}

			await using ServiceProvider services = BuildWebJobServices(connectionString, Override);
			await using ApiFactory api = new ApiFactory(Override);
			using HttpClient http = StartApi(api, connectionString);

			User[] users = await SeedAsync(services, recipient);
			TimeSpan spacing = live ? LiveSpacing : RehearsalSpacing;

			Progress($"NF-03 {phase}: {runs.Length} run(s), {spacing.TotalSeconds:F0}s apart. Evidence: {CsvPath}");
			if (!File.Exists(CsvPath))
			{
				await File.WriteAllTextAsync(CsvPath, "RunId,Phase,Trigger,UserId,EscalationTriggeredUtc,SmsConfirmedSentUtc,LatencySeconds," +
					"StopwatchSeconds,TwilioSid,TwilioStatus,Result" + Environment.NewLine);
			}

			List<Measurement> results = [];
			for (int i = 0; i < runs.Length; i++)
			{
				Run run = runs[i];
				User user = users[run.User];
				string label = $"Latency test {i + 1}/{runs.Length}";

				Measurement measurement;
				try
				{
					measurement = await RunOnceAsync(services, http, client, phase, run, user, label);
				}
				catch (Exception exception)
				{
					measurement = new Measurement(run, user.Id, null, null, null, null, null, exception.Message);
				}

				string result = measurement.Problem is not null ? "ABORTED"
					: phase == Phase.Rejection ? "REFUSED"
					: measurement.Elapsed <= Budget ? "PASS" : "FAIL";
				await File.AppendAllTextAsync(CsvPath, CsvRow(phase, measurement, result) + Environment.NewLine);
				Progress($"[{i + 1}/{runs.Length}] {run.Id} {run.Trigger}: {result} " + (measurement.Problem ??
					$"stopwatch {Seconds(measurement.Elapsed)}s, stored {Seconds(measurement.Sent - measurement.Triggered)}s, " +
					$"SID {measurement.Sid}, status {measurement.Status}"));

				if (measurement.Problem is not null)
				{
					Assert.Fail($"{run.Id} aborted the {phase} phase after {results.Count} completed run(s): {measurement.Problem}");
				}

				results.Add(measurement);

				if (i < runs.Length - 1)
				{
					await Task.Delay(spacing);
				}
			}

			if (phase == Phase.Rejection)
			{
				Progress($"NF-03 {phase}: Twilio refused all {results.Count} requests for the sender, and nothing was recorded. " +
					"Auth, the request path and the rollback on refusal all work.");
				return;
			}

			TimeSpan[] elapsed = results.Select(r => r.Elapsed!.Value).ToArray();
			TimeSpan maxLatency = elapsed.Max();
			string overBudgetList = string.Join(", ", results.Where(r => r.Elapsed > Budget).Select(r => r.Run.Id).DefaultIfEmpty("none"));

			Progress($"NF-03 {phase}: {results.Count} runs. Min {elapsed.Min().TotalSeconds:F2}s, mean " +
				$"{elapsed.Average(e => e.TotalSeconds):F2}s, max {maxLatency.TotalSeconds:F2}s, against a {Budget.TotalSeconds:F0}s budget." +
				(live ? $" Live messages sent by this run: {liveSends}." : ""));

			Assert.True(maxLatency <= TimeSpan.FromSeconds(60),
				$"NF-03 max latency {maxLatency.TotalSeconds:F2}s across {results.Count} runs. Over budget: {overBudgetList}");
		}

		/// <summary>
		/// One iteration: puts the user where the trigger needs them, fires it, and reads T0, T1, the SID and the status back from the
		/// database. Returns a <see cref="Measurement.Problem"/> rather than throwing once there is something worth recording.
		/// </summary>
		private static async Task<Measurement> RunOnceAsync(ServiceProvider services, HttpClient http, GuardedTwilioClient client,
			Phase phase, Run run, User user, string label)
		{
			using IServiceScope scope = services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			DateTimeOffset startedAt = DateTimeOffset.UtcNow;
			await ResetAsync(db, user.Id, run.Trigger, label);

			Stopwatch stopwatch;
			string? refused = null;
			if (run.Trigger == Trigger.EscalationLogic)
			{
				EscalationService service = scope.ServiceProvider.GetRequiredService<EscalationService>();

				// One WebJob run, as AmigoAlert.WebJob performs it, restricted to this user.
				IReadOnlyList<EscalationDecision> decisions = await service.EvaluateDueUsersAsync(DateTimeOffset.UtcNow);
				EscalationDecision decision = decisions.SingleOrDefault(d => d.UserId == user.Id
					&& d.Outcome == EscalationOutcome.EscalateToEmergencyContact)
					?? throw new InvalidOperationException($"No escalation decision for {user.Email}: the reset left it in the wrong state.");

				// T0: the decision is made. The poll that made it is not timed; NF-03 starts here.
				stopwatch = Stopwatch.StartNew();
				bool acted = await service.ActOnDecisionAsync(decision);
				stopwatch.Stop();

				if (!acted)
				{
					refused = "The escalation was not carried out";
				}
			}
			else
			{
				// The request the app's emergency button sends, here without a location fix.
				stopwatch = Stopwatch.StartNew();
				using HttpResponseMessage response = await http.PostAsJsonAsync($"api/users/{user.Id}/emergency",
					new ActivateEmergencyDto(null, null));
				stopwatch.Stop();

				if (response.StatusCode != HttpStatusCode.NoContent)
				{
					refused = $"The emergency endpoint returned {(int)response.StatusCode}";
				}
			}

			if (phase == Phase.Rejection)
			{
				return await CheckRejectionAsync(db, run, user.Id, client, refused, stopwatch.Elapsed, startedAt);
			}

			if (refused is not null)
			{
				return new Measurement(run, user.Id, null, null, stopwatch.Elapsed, null, null,
					$"{refused}. Twilio: {client.LastError ?? "no error response"}.");
			}

			byte raised = (byte)(run.Trigger == Trigger.EscalationLogic ? StatusType.Escalated : StatusType.Emergency);
			List<Evidence> rows = await db.Sms
				.Where(m => m.StatusLog != null && m.StatusLog.UserId == user.Id && m.StatusLog.StatusId == raised
					&& m.StatusLog.TimeOccurrence >= startedAt)
				.Select(m => new Evidence(m.StatusLog!.TimeOccurrence, m.TimeSent, m.TwilioSid, m.Status))
				.ToListAsync();

			if (rows.Count != 1)
			{
				return new Measurement(run, user.Id, null, null, stopwatch.Elapsed, null, null,
					$"Expected one Sms row for the {(StatusType)raised} log, found {rows.Count}.");
			}

			Evidence row = rows[0];
			string sid = row.TwilioSid?.Trim() ?? string.Empty;
			Measurement measurement = new Measurement(run, user.Id, row.Triggered, row.TimeSent, stopwatch.Elapsed, sid, row.Status, null);

			string? problem = null;
			if (phase == Phase.Mock ? row.Status != "mock" : !AcceptedStatuses.Contains(row.Status))
			{
				problem = $"Twilio status '{row.Status}' is not an acceptance.";
			}
			else if (phase != Phase.Mock && sid != client.LastSid)
			{
				problem = $"The stored SID {sid} is not the one Twilio returned ({client.LastSid}).";
			}
			else if ((stopwatch.Elapsed - (row.TimeSent - row.Triggered)).Duration() > ClockTolerance)
			{
				problem = "The stopwatch and the stored timestamps disagree by more than a second; investigate the clock before reporting.";
			}

			return measurement with { Problem = problem };
		}

		/// <summary>
		/// Phase 1's verdict on one run: Twilio refused the alert for its sender, not for any other reason, and the refusal rolled back
		/// everything, so the user is still where the reset left them and no alert log or <c>Sms</c> row claims the contact was told.
		/// </summary>
		private static async Task<Measurement> CheckRejectionAsync(AmigoAlertDbContext db, Run run, Guid userId, GuardedTwilioClient client,
			string? refused, TimeSpan elapsed, DateTimeOffset startedAt)
		{
			StatusType expected = run.Trigger == Trigger.EscalationLogic ? StatusType.Pending2 : StatusType.Okay;
			StatusType status = (StatusType)await db.Users.Where(u => u.Id == userId).Select(u => u.StatusId).SingleAsync();
			int alerts = await db.StatusLogs.CountAsync(l => l.UserId == userId && l.TimeOccurrence >= startedAt
				&& (l.StatusId == (byte)StatusType.Escalated || l.StatusId == (byte)StatusType.Emergency));
			int messages = await db.Sms.CountAsync(m => m.EmergencyContact.UserId == userId && m.TimeSent >= startedAt);

			string? problem = null;
			if (refused is null)
			{
				problem = $"Twilio accepted a message from {TestFromNumber} (SID {client.LastSid}). Stop and check which credentials are set.";
			}
			else if (client.LastErrorCode is not int code || !SenderRefusals.Contains(code))
			{
				problem = $"{refused}, but not for the sender. Twilio: {client.LastError ?? "no error response"}.";
			}
			else if (status != expected || alerts != 0 || messages != 0)
			{
				problem = $"The refused alert left state behind: status {status}, {alerts} alert log(s), {messages} Sms row(s).";
			}

			return new Measurement(run, userId, null, null, elapsed, null, client.LastErrorCode?.ToString(CultureInfo.InvariantCulture),
				problem);
		}

		/// <summary>
		/// Puts the user back where the trigger needs them, so a status left at Escalated or Emergency by an earlier run cannot suppress
		/// this one. The emergency button needs only Okay. Escalation needs an occurrence whose reminder window has run out: two
		/// unanswered check-ins and Pending2, exactly as the WebJob's own notification and reminder polls would have left them. Earlier
		/// <c>Sms</c> and <c>StatusLog</c> rows are kept; they are the stored evidence.
		/// </summary>
		private static async Task ResetAsync(AmigoAlertDbContext db, Guid userId, Trigger trigger, string label)
		{
			await db.CheckIns.Where(c => c.UserId == userId).ExecuteDeleteAsync();

			if (trigger == Trigger.EmergencyButton)
			{
				await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x
					.SetProperty(u => u.StatusId, (byte)StatusType.Okay)
					.SetProperty(u => u.Name, label)
					.SetProperty(u => u.Notification1Time, (TimeOnly?)null)
					.SetProperty(u => u.Notification2Time, (TimeOnly?)null));
				return;
			}

			// S: a whole local minute far enough back that both windows are over by now.
			DateTimeOffset local = DateTimeOffset.UtcNow.ToOffset(EscalationRules.LocalOffset);
			DateTimeOffset s = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, EscalationRules.LocalOffset)
				- T - TimeSpan.FromMinutes(1);

			await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.StatusId, (byte)StatusType.Pending2)
				.SetProperty(u => u.Name, label)
				.SetProperty(u => u.Notification1Time, (TimeOnly?)TimeOnly.FromDateTime(s.DateTime))
				.SetProperty(u => u.Notification2Time, (TimeOnly?)null));

			db.CheckIns.AddRange(
				new CheckIn { UserId = userId, ScheduledFor = s, TimeSent = s, ResponseId = (byte)CheckInResponseType.NoResponse },
				new CheckIn { UserId = userId, ScheduledFor = s, TimeSent = s + Reminder, ResponseId = (byte)CheckInResponseType.NoResponse });
			await db.SaveChangesAsync();
		}

		/// <summary>
		/// Runs Seed.sql and clears every seeded user's schedule. In the live phases every seeded contact is then pointed at the one
		/// permitted recipient, so none of the seed's made-up numbers can be dialled.
		/// </summary>
		private static async Task<User[]> SeedAsync(ServiceProvider services, string? recipient)
		{
			using IServiceScope scope = services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Accuracy", "Seed.sql"));
			await db.Database.ExecuteSqlRawAsync(sql);

			await db.Users.Where(u => u.Email.EndsWith(SeedEmailDomain)).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.Notification1Time, (TimeOnly?)null)
				.SetProperty(u => u.Notification2Time, (TimeOnly?)null));

			if (recipient is not null)
			{
				await db.EmergencyContacts.Where(c => c.User.Email.EndsWith(SeedEmailDomain))
					.ExecuteUpdateAsync(x => x.SetProperty(c => c.Phone, recipient));
			}

			return await db.Users
				.Where(u => u.Email.EndsWith(SeedEmailDomain))
				.OrderBy(u => u.Email)
				.ToArrayAsync();
		}

		/// <summary>The WebJob's composition, data layer and services, with the phase's overrides on top.</summary>
		private static ServiceProvider BuildWebJobServices(string connectionString, Action<IServiceCollection> configure)
		{
			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddAmigoAlertData(connectionString);
			services.AddAmigoAlertServices(new ConfigurationBuilder().Build());
			configure(services);

			return services.BuildServiceProvider();
		}

		/// <summary>
		/// Starts the API in process. Program.Main reads the connection string and the vault URI while it builds, before any test override
		/// can apply, so those two go in as environment variables for the duration of the start. A blank vault URI keeps the API from loading
		/// the production Twilio credentials from Key Vault; it is a single space because Windows deletes a variable set to empty.
		/// </summary>
		private static HttpClient StartApi(ApiFactory api, string connectionString)
		{
			Dictionary<string, string> overrides = new Dictionary<string, string>
			{
				["ConnectionStrings__AmigoAlertDb"] = connectionString,
				["KeyVault__VaultUri"] = " "
			};
			Dictionary<string, string?> previous = overrides.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

			try
			{
				foreach ((string key, string value) in overrides)
				{
					Environment.SetEnvironmentVariable(key, value);
				}

				return api.CreateClient();
			}
			finally
			{
				foreach ((string key, string? value) in previous)
				{
					Environment.SetEnvironmentVariable(key, value);
				}
			}
		}

		/// <summary>The credentials for the phase: none for the mock, otherwise read from the environment and never from production config.</summary>
		private static TwilioOptions TwilioSettings(Phase phase)
		{
			if (phase == Phase.Mock)
			{
				return new TwilioOptions();
			}

			return new TwilioOptions
			{
				AccountSid = Require(AccountSidVariable, "the Twilio account SID"),
				AuthToken = Require(AuthTokenVariable, "the Twilio auth token"),
				// Phase 1 sends no message; GuardedTwilioClient replaces the Messaging Service with the magic sender.
				MessagingServiceSid = phase == Phase.Rejection
					? string.Empty
					: Require(MessagingServiceVariable, "the Messaging Service SID live messages are sent through")
			};
		}

		private bool LiveEnabled()
		{
			if (Environment.GetEnvironmentVariable(LiveVariable) == "1")
			{
				return true;
			}

			output.WriteLine($"Skipped: sends real SMS. Set {LiveVariable}=1 to run it.");
			return false;
		}

		private static string Require(string variable, string description)
		{
			string? value = Environment.GetEnvironmentVariable(variable);
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidOperationException($"Set {variable} to {description}.");
			}

			return value.Trim();
		}

		/// <summary>To the test output, and to the console as it happens, so a stalled live run is obvious.</summary>
		private void Progress(string message)
		{
			output.WriteLine(message);
			Console.WriteLine(message);
		}

		private static string CsvRow(Phase phase, Measurement m, string result)
		{
			return string.Join(",", m.Run.Id, phase, m.Run.Trigger, m.UserId, Iso(m.Triggered), Iso(m.Sent), Seconds(m.Sent - m.Triggered),
				Seconds(m.Elapsed), m.Sid ?? "", m.Status ?? "", result);
		}

		private static string Iso(DateTimeOffset? instant)
		{
			return instant?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) ?? "";
		}

		private static string Seconds(TimeSpan? interval)
		{
			return interval?.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) ?? "";
		}

		/// <summary>One scored or rehearsal run: its id from the brief, how it fires, and which seeded user it uses.</summary>
		private sealed record Run(string Id, Trigger Trigger, int User);

		/// <summary>The stored record of one alert: T0 from its StatusLog row, then T1, the SID and the status from its Sms row.</summary>
		private sealed record Evidence(DateTimeOffset Triggered, DateTimeOffset TimeSent, string? TwilioSid, string? Status);

		/// <summary>What one run measured. Everything but the run and user is null when the run aborted before it was known.</summary>
		/// <param name="Triggered">T0, read back from the StatusLog row.</param>
		/// <param name="Sent">T1, read back from the Sms row.</param>
		/// <param name="Elapsed">The stopwatch interval; the authoritative latency.</param>
		/// <param name="Problem">Why the phase must stop here, or null.</param>
		private sealed record Measurement(Run Run, Guid UserId, DateTimeOffset? Triggered, DateTimeOffset? Sent, TimeSpan? Elapsed,
			string? Sid, string? Status, string? Problem);

		/// <summary>The API host, with the same overrides as the WebJob composition.</summary>
		private sealed class ApiFactory(Action<IServiceCollection> configure) : WebApplicationFactory<AmigoAlert_API.Program>
		{
			protected override void ConfigureWebHost(IWebHostBuilder builder)
			{
				// The API logs every SQL command at Information, which buries the per-run progress lines.
				builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
				builder.ConfigureTestServices(configure);
			}
		}

		/// <summary>
		/// The only route to Twilio in this suite, wrapped round the real client. In the mock phase it refuses every request. In phase 1 it
		/// swaps the Messaging Service for the magic sender, which the live account cannot send from. Live, it refuses any recipient but the permitted one and
		/// counts the send against <see cref="MaxLiveSends"/> before it leaves. It also keeps the SID Twilio returned, so the stored row can
		/// be proved to be the message that was timed.
		/// </summary>
		/// <param name="inner">The real client, or null in the mock phase.</param>
		/// <param name="testFrom">The magic sender in phase 1; null when live.</param>
		/// <param name="recipient">The one permitted live recipient; null unless live.</param>
		private sealed class GuardedTwilioClient(ITwilioRestClient? inner, string? testFrom, string? recipient) : ITwilioRestClient
		{
			public string? LastSid { get; private set; }

			/// <summary>Why Twilio refused the last request, which SmsService logs but does not return.</summary>
			public string? LastError { get; private set; }

			/// <summary>Twilio's error code for the last refusal, or null.</summary>
			public int? LastErrorCode { get; private set; }

			public string AccountSid => Inner.AccountSid;

			public string Region => Inner.Region;

			public Twilio.Http.HttpClient HttpClient => Inner.HttpClient;

			private ITwilioRestClient Inner => inner ?? throw new InvalidOperationException("The mock phase reached Twilio.");

			public Twilio.Http.Response Request(Twilio.Http.Request request)
			{
				throw new NotSupportedException("SmsService sends asynchronously; a synchronous send is unexpected.");
			}

			public async Task<Twilio.Http.Response> RequestAsync(Twilio.Http.Request request)
			{
				ITwilioRestClient client = Inner;
				string? to = request.PostParams.FirstOrDefault(p => p.Key == "To").Value;

				if (testFrom is not null)
				{
					request.PostParams.RemoveAll(p => p.Key is "MessagingServiceSid" or "From");
					request.PostParams.Add(new KeyValuePair<string, string>("From", testFrom));
				}
				else if (recipient is null || to != recipient)
				{
					throw new InvalidOperationException($"Refused a live SMS to {to}: only {RecipientVariable} may be messaged.");
				}
				else if (Interlocked.Increment(ref liveSends) > MaxLiveSends)
				{
					throw new InvalidOperationException($"Refused a live SMS: the cap of {MaxLiveSends} for this run is reached.");
				}

				LastSid = null;
				LastError = null;
				LastErrorCode = null;
				Twilio.Http.Response response;
				try
				{
					response = await client.RequestAsync(request).WaitAsync(TwilioTimeout);
				}
				catch (ApiException exception)
				{
					// The client throws on any refusal; recorded here, then rethrown so SmsService handles it exactly as in production.
					LastError = $"HTTP {exception.Status}, error {exception.Code}: {exception.Message} {exception.MoreInfo}";
					LastErrorCode = exception.Code;
					throw;
				}

				using JsonDocument json = JsonDocument.Parse(response.Content);
				LastSid = json.RootElement.TryGetProperty("sid", out JsonElement sid) ? sid.GetString() : null;

				return response;
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
