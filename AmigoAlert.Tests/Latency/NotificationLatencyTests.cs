using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Services.Results;
using AmigoAlert.Services.Rules;
using AmigoAlert.Shared.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Xunit.Abstractions;

namespace AmigoAlert.Tests.Latency
{
	/// <summary>
	/// NF-04 evidence: at least 90% of check-in notifications reach the device within 2 minutes of the scheduled time. Twenty check-ins one
	/// minute apart, each timed from its scheduled instant to the instant the phone reports it arrived. Writes one timestamped CSV, a row as
	/// each delivery lands.
	/// </summary>
	/// <remarks>
	/// The test only orchestrates and observes: the real WebJob, running beside it, picks each check-in up and pushes it. The scheduled time
	/// is <see cref="CheckIn.ScheduledFor"/>. The push time is <see cref="StatusLog.TimeOccurrence"/> of the "Check-in notification sent."
	/// row, stamped just before the push (<see cref="CheckIn.TimeSent"/> is deliberately rounded back to the minute, so it is not used).
	/// The delivery time is <see cref="CheckIn.TimeDelivered"/>, which the phone reports by its own clock as soon as the push arrives.
	/// <para>
	/// A user has only two daily check-in times, so the schedule leapfrogs: slot 1 holds the next trial and slot 2 the one just sent. Slot 2
	/// must never be empty, or the rules would look back to yesterday's occurrence of slot 1 and send that. Each check-in is answered as
	/// soon as it is sent, as the app's "I'm okay" would, so every cycle starts from Okay and none waits on the previous delivery.
	/// </para>
	/// <para>
	/// Opt-in: runs only with <c>AMIGOALERT_NF04_USER_ID</c> set to the user id the phone is registered as, and <c>AMIGOALERT_TEST_SQL</c>
	/// pointed at a migrated test database. <see cref="Prepare"/> adds that user to the test database and sends one unscored check-in to
	/// prove the whole path; <see cref="Run"/> is the scored run, about 27 minutes. The WebJob, the API and this test must all use the same
	/// database, with <c>Twilio__UseMockSms=true</c> so no Twilio client exists anywhere, and the phone must run a Debug build with
	/// <c>ApiSettings.UseLocalApi = true</c> so it registers through the local API and reports deliveries to it.
	/// </para>
	/// </remarks>
	public sealed class NotificationLatencyTests(ITestOutputHelper output)
	{
		private const string ConnectionVariable = "AMIGOALERT_TEST_SQL";

		private const string UserVariable = "AMIGOALERT_NF04_USER_ID";

		/// <summary>The production database; the run rewrites the user's schedule, so it refuses to go near it.</summary>
		private const string ProductionDatabase = "AmigoAlertDB";

		/// <summary>The notes EscalationService writes on the status log row of a first notification, stamped just before the push.</summary>
		private const string SentNote = "Check-in notification sent.";

		private const int TrialCount = 20;

		private static readonly TimeSpan Budget = TimeSpan.FromMinutes(2);

		private static readonly TimeSpan Spacing = TimeSpan.FromMinutes(1);

		/// <summary>From the start of the run to the first trial: time to set the schedule, and to launch the app before the smoke trial.</summary>
		private static readonly TimeSpan Lead = TimeSpan.FromMinutes(2);

		private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

		/// <summary>How long after the last trial deliveries are still waited for.</summary>
		private static readonly TimeSpan Tail = TimeSpan.FromMinutes(5);

		/// <summary>A trial not sent this long after its time means the WebJob is not running against this database.</summary>
		private static readonly TimeSpan SendDeadline = TimeSpan.FromMinutes(3);

		private static readonly string CsvPath = Path.Combine(AppContext.BaseDirectory,
			$"nf04-notification-latency-{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.csv");

		/// <summary>
		/// Adds the phone's user to the test database, then sends one unscored check-in through the real WebJob and waits for the phone to
		/// report it. Run first, then launch the app so it registers under that user; rerun until it passes.
		/// </summary>
		[Fact]
		public async Task Prepare()
		{
			if (!Enabled(out Guid userId, out string connectionString))
			{
				return;
			}

			Progress($"NF-04 prepare: user {userId}. Launch or relaunch the app now, so it registers through the local API before the " +
				"smoke check-in is sent.");

			Trial[] trials = await RunTrialsAsync(connectionString, userId, "S", 1, csvPath: null);
			Trial smoke = trials[0];

			Assert.True(smoke.Delivered is not null, "NF-04 prepare: the smoke check-in was pushed but the phone never reported it. Check " +
				"the device preconditions, that the app registered with the dev hub, and that the phone can reach the API.");

			Progress($"NF-04 prepare: delivered in {Seconds(smoke.Latency)}s. Read the API log line for check-in {smoke.CheckInId}: the " +
				"server-minus-device difference must be small and positive. If it is negative or over 2 seconds, fix the device clock first.");
		}

		/// <summary>The scored run: twenty check-ins, at least eighteen of them delivered within two minutes.</summary>
		[Fact]
		public async Task Run()
		{
			if (!Enabled(out Guid userId, out string connectionString))
			{
				return;
			}

			Progress($"NF-04: {TrialCount} check-ins, {Spacing.TotalSeconds:F0}s apart, for user {userId}. Evidence: {CsvPath}");
			await File.WriteAllTextAsync(CsvPath,
				"TrialId,CheckInId,ScheduledUtc,PushSentUtc,ReceivedOnDeviceUtc,LatencySeconds,PollDelaySeconds,WithinTwoMinutes" +
				Environment.NewLine);

			Trial[] trials = await RunTrialsAsync(connectionString, userId, "N", TrialCount, CsvPath);

			// The trials that never landed are misses; they are written last, since there was nothing to write as they happened.
			foreach (Trial trial in trials.Where(t => t.Delivered is null))
			{
				await File.AppendAllTextAsync(CsvPath, CsvRow(trial) + Environment.NewLine);
			}

			Trial[] delivered = trials.Where(t => t.Delivered is not null).ToArray();
			int counted = trials.Length;
			int withinBudget = trials.Count(t => t.Latency <= Budget);
			string overBudgetList = string.Join(", ", trials.Where(t => !(t.Latency <= Budget))
				.Select(t => t.Latency is null ? $"{t.Id} (no receipt)" : $"{t.Id} ({Seconds(t.Latency)}s)").DefaultIfEmpty("none"));

			if (delivered.Length > 0)
			{
				Progress($"NF-04: {withinBudget}/{counted} within {Budget.TotalSeconds:F0}s. Latency mean " +
					$"{delivered.Average(t => t.Latency!.Value.TotalSeconds):F2}s, max {delivered.Max(t => t.Latency!.Value).TotalSeconds:F2}s. " +
					$"Poll delay mean {delivered.Average(t => t.PollDelay!.Value.TotalSeconds):F2}s, max " +
					$"{delivered.Max(t => t.PollDelay!.Value).TotalSeconds:F2}s. Evidence: {CsvPath}");
			}

			if (withinBudget == (int)Math.Ceiling(counted * 0.9))
			{
				Progress("NF-04: passed with no headroom to spare; repeat the run before reporting it.");
			}

			Assert.True(withinBudget / (double)counted >= 0.90,
				$"NF-04: {withinBudget}/{counted} within 120s. Over budget: {overBudgetList}");
		}

		/// <summary>
		/// Resets the user, schedules <paramref name="count"/> check-ins one minute apart, and polls until every one has been delivered or the
		/// tail after the last has run out. The schedule is cleared however it ends, so the WebJob stops acting on the user.
		/// </summary>
		private async Task<Trial[]> RunTrialsAsync(string connectionString, Guid userId, string prefix, int count, string? csvPath)
		{
			await using ServiceProvider services = BuildServices(connectionString);

			// Baseline: the current local minute, already "answered", so slot 2 has an occurrence the rules consider closed.
			DateTimeOffset local = DateTimeOffset.UtcNow.ToOffset(EscalationRules.LocalOffset);
			DateTimeOffset baseline = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0,
				EscalationRules.LocalOffset);
			Trial[] trials = Enumerable.Range(1, count)
				.Select(i => new Trial($"{prefix}-{i:D2}", baseline + Lead + Spacing * (i - 1)))
				.ToArray();

			await ResetAsync(services, userId, baseline, trials[0].ScheduledFor);
			Progress($"Trials {trials[0].Id} at {Iso(trials[0].ScheduledFor)} to {trials[^1].Id} at {Iso(trials[^1].ScheduledFor)}; " +
				$"polling every {PollInterval.TotalSeconds:F0}s.");

			DateTimeOffset deadline = trials[^1].ScheduledFor + Tail;
			int next = 0;

			try
			{
				while (true)
				{
					using IServiceScope scope = services.CreateScope();
					AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();
					DateTimeOffset now = DateTimeOffset.UtcNow;

					// The WebJob sends at most one trial per run, and the next one is only scheduled once this one is out.
					if (next < trials.Length && await TrySendAsync(scope, db, userId, trials, next))
					{
						next++;
					}

					if (next < trials.Length && now > trials[next].ScheduledFor + SendDeadline)
					{
						Assert.Fail($"{trials[next].Id} was due at {Iso(trials[next].ScheduledFor)} and has not been sent. Is the WebJob loop " +
							"running against this database?");
					}

					foreach (Trial trial in await NewDeliveriesAsync(db, trials))
					{
						Progress($"{trial.Id}: delivered {Seconds(trial.Latency)}s after its scheduled time (poll delay " +
							$"{Seconds(trial.PollDelay)}s, push to device {Seconds(trial.Delivered - trial.PushSent)}s).");
						if (csvPath is not null)
						{
							await File.AppendAllTextAsync(csvPath, CsvRow(trial) + Environment.NewLine);
						}
					}

					if (trials.Length >= 2 && now > trials[1].ScheduledFor + Budget && trials.All(t => t.Delivered is null))
					{
						Assert.Fail("The first two check-ins were pushed but the phone reported neither. Run Prepare until it passes, then " +
							"start again.");
					}

					int delivered = trials.Count(t => t.Delivered is not null);
					if (delivered == trials.Length || now > deadline)
					{
						break;
					}

					Progress($"{now.UtcDateTime:HH:mm:ss}Z sent {next}/{trials.Length}, delivered {delivered}/{trials.Length}.");
					await Task.Delay(PollInterval);
				}
			}
			finally
			{
				await ClearScheduleAsync(services, userId);
			}

			await ReportSmsAsync(services, userId, baseline);
			return trials;
		}

		/// <summary>
		/// If the WebJob has sent trial <paramref name="index"/>, records it, answers it as the app would, and moves the schedule on to the
		/// next trial. Returns false while the trial is still waiting for the WebJob.
		/// </summary>
		private async Task<bool> TrySendAsync(IServiceScope scope, AmigoAlertDbContext db, Guid userId, Trial[] trials, int index)
		{
			Trial trial = trials[index];
			CheckIn? checkIn = await db.CheckIns.AsNoTracking()
				.Where(c => c.UserId == userId && c.ScheduledFor == trial.ScheduledFor)
				.OrderBy(c => c.Id)
				.FirstOrDefaultAsync();
			if (checkIn is null)
			{
				return false;
			}

			// The status log row and TimeSent come from the same instant, TimeSent rounded back to just before its minute.
			DateTimeOffset upper = checkIn.TimeSent.AddMinutes(1);
			trial.CheckInId = checkIn.Id;
			trial.PushSent = await db.StatusLogs.AsNoTracking()
				.Where(l => l.UserId == userId && l.Notes == SentNote && l.TimeOccurrence > checkIn.TimeSent && l.TimeOccurrence <= upper)
				.OrderBy(l => l.TimeOccurrence)
				.Select(l => (DateTimeOffset?)l.TimeOccurrence)
				.FirstOrDefaultAsync()
				?? throw new InvalidOperationException($"{trial.Id}: check-in {checkIn.Id} has no '{SentNote}' status log row.");

			// Already answered means the user tapped "I'm okay" on the phone first, which closes the cycle just the same.
			EscalationService escalation = scope.ServiceProvider.GetRequiredService<EscalationService>();
			CheckInResponseOutcome outcome = await escalation.RecordCheckInResponseAsync(checkIn.Id, CheckInResponseType.Okay);
			if (outcome is not (CheckInResponseOutcome.Recorded or CheckInResponseOutcome.AlreadyAnswered))
			{
				throw new InvalidOperationException($"{trial.Id}: answering check-in {checkIn.Id} returned {outcome}.");
			}

			if (index + 1 < trials.Length)
			{
				await SetScheduleAsync(db, userId, next: trials[index + 1].ScheduledFor, previous: trial.ScheduledFor);
			}

			Progress($"{trial.Id}: sent as check-in {checkIn.Id} at {Iso(trial.PushSent)}, {Seconds(trial.PollDelay)}s after its scheduled " +
				"time; answered.");
			return true;
		}

		/// <summary>
		/// The trials whose delivery the phone has reported since the last poll. A delivery timestamped before its push is impossible, so it
		/// proves the device clock is behind the server's and stops the run.
		/// </summary>
		private static async Task<Trial[]> NewDeliveriesAsync(AmigoAlertDbContext db, Trial[] trials)
		{
			long[] waiting = trials.Where(t => t.CheckInId is not null && t.Delivered is null).Select(t => t.CheckInId!.Value).ToArray();
			if (waiting.Length == 0)
			{
				return [];
			}

			Dictionary<long, DateTimeOffset> reported = await db.CheckIns.AsNoTracking()
				.Where(c => waiting.Contains(c.Id) && c.TimeDelivered != null)
				.ToDictionaryAsync(c => c.Id, c => c.TimeDelivered!.Value);

			List<Trial> landed = [];
			foreach (Trial trial in trials.Where(t => t.CheckInId is long id && reported.ContainsKey(id)))
			{
				trial.Delivered = reported[trial.CheckInId!.Value];
				if (trial.Delivered < trial.PushSent)
				{
					Assert.Fail($"{trial.Id}: the phone reports delivery at {Iso(trial.Delivered)}, before the push at {Iso(trial.PushSent)}. " +
						"The device clock is behind the server's; turn on automatic date and time, and start again.");
				}

				landed.Add(trial);
			}

			return [.. landed];
		}

		/// <summary>
		/// Runs Seed.sql, which leaves every seeded user unscheduled so the WebJob ignores them, then puts the phone's user in place with a
		/// consenting contact (the WebJob only evaluates users who have one), no earlier check-ins, status Okay, and the first trial scheduled.
		/// </summary>
		private static async Task ResetAsync(ServiceProvider services, Guid userId, DateTimeOffset baseline, DateTimeOffset first)
		{
			using IServiceScope scope = services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Accuracy", "Seed.sql"));
			await db.Database.ExecuteSqlRawAsync(sql);

			DateTimeOffset now = DateTimeOffset.UtcNow;
			if (!await db.Users.AnyAsync(u => u.Id == userId))
			{
				db.Users.Add(new User
				{
					Id = userId,
					Name = "NF-04 Device User",
					Email = $"nf04-{userId:N}@latency.test",
					Phone = "+593980000400",
					Address = "4 Latency St., Cotacachi, Ecuador",
					StatusId = (byte)StatusType.Okay,
					AcceptedTermsTime = now,
					AcceptedTermsVersion = "1.0"
				});
			}

			if (!await db.EmergencyContacts.AnyAsync(c => c.UserId == userId && c.IsActive && c.ConsentStatusId == (byte)ConsentStatusType.Granted))
			{
				db.EmergencyContacts.Add(new EmergencyContact
				{
					Id = Guid.NewGuid(),
					UserId = userId,
					Name = "NF-04 Contact",
					Phone = "+593990000400",
					ConsentStatusId = (byte)ConsentStatusType.Granted,
					ConsentRequestedAt = now,
					ConsentRespondedAt = now,
					IsActive = true
				});
			}

			await db.SaveChangesAsync();

			// Unscheduled before anything else changes, so a WebJob run in between sees nothing to do.
			await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.Notification1Time, (TimeOnly?)null)
				.SetProperty(u => u.Notification2Time, (TimeOnly?)null)
				.SetProperty(u => u.StatusId, (byte)StatusType.Okay));
			await db.CheckIns.Where(c => c.UserId == userId).ExecuteDeleteAsync();

			db.CheckIns.Add(new CheckIn
			{
				UserId = userId,
				ScheduledFor = baseline,
				TimeSent = baseline,
				TimeReceived = baseline,
				ResponseId = (byte)CheckInResponseType.Okay
			});
			await db.SaveChangesAsync();

			await SetScheduleAsync(db, userId, next: first, previous: baseline);
		}

		/// <summary>Both slots in one statement, so no WebJob run can see the schedule half moved.</summary>
		private static Task SetScheduleAsync(AmigoAlertDbContext db, Guid userId, DateTimeOffset next, DateTimeOffset previous)
		{
			return db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.Notification1Time, (TimeOnly?)TimeOnly.FromDateTime(next.ToOffset(EscalationRules.LocalOffset).DateTime))
				.SetProperty(u => u.Notification2Time, (TimeOnly?)TimeOnly.FromDateTime(previous.ToOffset(EscalationRules.LocalOffset).DateTime)));
		}

		private static async Task ClearScheduleAsync(ServiceProvider services, Guid userId)
		{
			using IServiceScope scope = services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(x => x
				.SetProperty(u => u.Notification1Time, (TimeOnly?)null)
				.SetProperty(u => u.Notification2Time, (TimeOnly?)null));
		}

		/// <summary>
		/// Every cycle is answered within the minute, so nothing should ever reach the reminder, let alone escalate. Any SMS row for the user
		/// means a cycle went wrong; a non-mock one means the WebJob was not running with the mock.
		/// </summary>
		private async Task ReportSmsAsync(ServiceProvider services, Guid userId, DateTimeOffset since)
		{
			using IServiceScope scope = services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			string?[] sids = await db.Sms.AsNoTracking()
				.Where(m => m.EmergencyContact.UserId == userId && m.TimeSent >= since)
				.Select(m => m.TwilioSid)
				.ToArrayAsync();

			Progress($"SMS rows for the user during the run: {sids.Length}" + (sids.Length == 0 ? "." : $" ({string.Join(", ", sids)})."));
			Assert.True(sids.All(s => s?.StartsWith(MockSmsService.SidPrefix, StringComparison.Ordinal) == true),
				"A real SMS was recorded during the run: the WebJob or the API is not running with Twilio__UseMockSms=true.");
		}

		/// <summary>
		/// The data layer and services, with the mock SMS service switched on through configuration exactly as the WebJob's is, and a push
		/// service that refuses to send, so nothing this test does can reach Twilio or the phone.
		/// </summary>
		private static ServiceProvider BuildServices(string connectionString)
		{
			IConfiguration configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["Twilio:UseMockSms"] = "true" })
				.Build();

			ServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddAmigoAlertData(connectionString);
			services.AddAmigoAlertServices(configuration);
			services.AddSingleton<IPushNotificationService, NoPush>();

			return services.BuildServiceProvider();
		}

		private bool Enabled(out Guid userId, out string connectionString)
		{
			userId = Guid.Empty;
			connectionString = string.Empty;

			string? user = Environment.GetEnvironmentVariable(UserVariable);
			if (string.IsNullOrWhiteSpace(user))
			{
				output.WriteLine($"Skipped: needs the phone, the WebJob and the API. Set {UserVariable} to the phone's user id to run it.");
				return false;
			}

			if (!Guid.TryParse(user, out userId))
			{
				throw new InvalidOperationException($"{UserVariable} must be a user id (a GUID), not '{user}'.");
			}

			connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)?.Trim() ?? string.Empty;
			if (connectionString.Length == 0)
			{
				throw new InvalidOperationException($"Set {ConnectionVariable} to the connection string of a migrated test database.");
			}

			if (string.Equals(new SqlConnectionStringBuilder(connectionString).InitialCatalog, ProductionDatabase, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"{ConnectionVariable} points at {ProductionDatabase}; NF-04 runs against a test database only.");
			}

			return true;
		}

		/// <summary>To the test output, and to the console as it happens, so a stalled run is obvious within a minute.</summary>
		private void Progress(string message)
		{
			output.WriteLine(message);
			Console.WriteLine(message);
		}

		private static string CsvRow(Trial t)
		{
			return string.Join(",", t.Id, t.CheckInId?.ToString(CultureInfo.InvariantCulture) ?? "", Iso(t.ScheduledFor), Iso(t.PushSent),
				Iso(t.Delivered), Seconds(t.Latency), Seconds(t.PollDelay), t.Latency <= Budget ? "TRUE" : "FALSE");
		}

		private static string Iso(DateTimeOffset? instant)
		{
			return instant?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) ?? "";
		}

		private static string Seconds(TimeSpan? interval)
		{
			return interval?.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) ?? "";
		}

		/// <summary>One scheduled check-in and what was observed of it; the observations stay null until they happen.</summary>
		private sealed class Trial(string id, DateTimeOffset scheduledFor)
		{
			public string Id { get; } = id;

			public DateTimeOffset ScheduledFor { get; } = scheduledFor;

			public long? CheckInId { get; set; }

			public DateTimeOffset? PushSent { get; set; }

			public DateTimeOffset? Delivered { get; set; }

			/// <summary>Scheduled to delivered: the NF-04 measurement.</summary>
			public TimeSpan? Latency => Delivered - ScheduledFor;

			/// <summary>Scheduled to pushed: the WebJob's scheduling cadence, kept apart from the push path.</summary>
			public TimeSpan? PollDelay => PushSent - ScheduledFor;
		}

		/// <summary>The test never pushes; the WebJob does.</summary>
		private sealed class NoPush : IPushNotificationService
		{
			public Task<bool> RegisterDeviceAsync(Guid userId, Guid installationId, string pushChannel, CancellationToken ct = default)
			{
				throw new InvalidOperationException("NF-04 must not register devices.");
			}

			public Task<bool> SendCheckInAsync(Guid userId, long checkInId, bool isReminder, DateTimeOffset sentAt,
				CancellationToken ct = default)
			{
				throw new InvalidOperationException("NF-04 must not push; the WebJob sends every check-in.");
			}
		}
	}
}
