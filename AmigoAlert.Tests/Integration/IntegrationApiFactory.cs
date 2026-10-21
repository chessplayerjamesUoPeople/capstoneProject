using AmigoAlert.Data;
using AmigoAlert.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// The integration test classes share one database, so they run as one collection, one at a time, against one API host.
	/// </summary>
	[CollectionDefinition(Name)]
	public sealed class IntegrationCollection : ICollectionFixture<IntegrationApiFactory>
	{
		public const string Name = "Integration";
	}

	/// <summary>
	/// The real API, services, EF Core and SQL Server, pointed at the <c>AMIGOALERT_TEST_SQL</c> database with
	/// <see cref="MockSmsService"/> in place of Twilio. Nothing else is replaced.
	/// </summary>
	/// <remarks>
	/// <c>Twilio:UseMockSms</c> is switched on through configuration, exactly as for a local run, so no Twilio client is registered and Key
	/// Vault is never read. <see cref="MockSmsService"/> is scoped, because it writes its <c>Sms</c> row through the request's own
	/// repository and transaction; a singleton would hold a disposed DbContext after the first request. Every instance the host creates is
	/// kept here instead, so <see cref="SentSms"/> sees every message the API sent.
	/// </remarks>
	public sealed class IntegrationApiFactory : WebApplicationFactory<AmigoAlert_API.Program>
	{
		private const string ConnectionVariable = "AMIGOALERT_TEST_SQL";

		/// <summary>
		/// The email domain of every user the integration tests register. Distinct from Seed.sql's <c>@amigoalert.test</c>, which
		/// AccuracyTests counts on being exactly its six users.
		/// </summary>
		public const string RegisteredEmailDomain = "@integration.test";

		private readonly ConcurrentQueue<MockSmsService> smsServices = new ConcurrentQueue<MockSmsService>();

		private bool migrated;

		public string ConnectionString { get; } = Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } value
			? value
			: throw new InvalidOperationException($"Set {ConnectionVariable} to the connection string of a test database (LocalDB or a " +
				"dedicated Azure SQL test database, never the real one). It is migrated on first use.");

		/// <summary>Every SMS sent since the last <see cref="ResetAsync"/>, oldest first.</summary>
		public IReadOnlyList<MockSmsService.SentMessage> SentSms => smsServices.SelectMany(s => s.Sent).OrderBy(m => m.SentAt).ToList();

		/// <summary>
		/// Replaces the <see cref="ISmsService"/> registration in <paramref name="services"/> with a scoped <see cref="MockSmsService"/>
		/// whose messages appear in <see cref="SentSms"/>. Used by the API host and by the WebJob tests' own container.
		/// </summary>
		public void RecordSms(IServiceCollection services)
		{
			services.RemoveAll<ISmsService>();
			services.AddScoped<ISmsService>(provider =>
			{
				MockSmsService sms = ActivatorUtilities.CreateInstance<MockSmsService>(provider);
				smsServices.Enqueue(sms);
				return sms;
			});
		}

		/// <summary>
		/// Migrates the test database on first use, re-runs Seed.sql, removes any user an integration test registered, and forgets
		/// recorded SMS. Called before every test, so no test depends on another's leftovers or on the order they run in.
		/// </summary>
		public async Task ResetAsync()
		{
			using IServiceScope scope = Services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			if (!migrated)
			{
				// Program reads the connection string itself; if the override ever stops reaching it, stop before writing anywhere else.
				SqlConnectionStringBuilder expected = new SqlConnectionStringBuilder(ConnectionString);
				SqlConnectionStringBuilder actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
				Assert.Equal((expected.DataSource, expected.InitialCatalog), (actual.DataSource, actual.InitialCatalog));
				await db.Database.MigrateAsync();
				migrated = true;
			}

			string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Accuracy", "Seed.sql"));
			await db.Database.ExecuteSqlRawAsync(sql);
			await RemoveRegisteredUsersAsync();

			smsServices.Clear();
		}

		/// <summary>Deletes every user registered by an integration test, with every row hanging off them.</summary>
		public async Task RemoveRegisteredUsersAsync()
		{
			using IServiceScope scope = Services.CreateScope();
			AmigoAlertDbContext db = scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>();

			await db.Sms.Where(m => m.EmergencyContact.User.Email.EndsWith(RegisteredEmailDomain)).ExecuteDeleteAsync();
			await db.StatusLogs.Where(l => l.User.Email.EndsWith(RegisteredEmailDomain)).ExecuteDeleteAsync();
			await db.CheckIns.Where(c => c.User.Email.EndsWith(RegisteredEmailDomain)).ExecuteDeleteAsync();
			await db.EmergencyContacts.Where(c => c.User.Email.EndsWith(RegisteredEmailDomain)).ExecuteDeleteAsync();
			await db.Users.Where(u => u.Email.EndsWith(RegisteredEmailDomain)).ExecuteDeleteAsync();
		}

		/// <summary>
		/// Runs <paramref name="query"/> in a new scope, so a read sees what reached the database rather than what a request's change tracker
		/// holds.
		/// </summary>
		public async Task<T> InNewScopeAsync<T>(Func<AmigoAlertDbContext, Task<T>> query)
		{
			using IServiceScope scope = Services.CreateScope();
			return await query(scope.ServiceProvider.GetRequiredService<AmigoAlertDbContext>());
		}

		/// <summary>
		/// Asserts that <paramref name="json"/> has exactly the properties of <typeparamref name="T"/>, camel-cased, so a renamed, added
		/// or leaked field fails here rather than arriving as a silent null on the device.
		/// </summary>
		public static void AssertShape<T>(JsonElement json)
		{
			string[] expected = typeof(T).GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).Order().ToArray();
			string[] actual = json.EnumerateObject().Select(p => p.Name).Order().ToArray();

			Assert.Equal(expected, actual);
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseSetting("ConnectionStrings:AmigoAlertDb", ConnectionString);
			builder.UseSetting("Twilio:UseMockSms", "true");

			// The API logs every SQL command at Information, which buries the test output.
			builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
			builder.ConfigureTestServices(RecordSms);
		}
	}
}
