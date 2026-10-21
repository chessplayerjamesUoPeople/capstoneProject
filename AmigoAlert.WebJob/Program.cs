using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AmigoAlert.WebJob
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			var builder = Host.CreateApplicationBuilder(args);

				// Pulls the Twilio credentials from Key Vault, over the empty placeholders in appsettings.json.
				builder.Configuration.AddAmigoAlertKeyVault();

			builder.Services.AddAmigoAlertData(builder.Configuration.GetConnectionString("AmigoAlertDb")!);
			builder.Services.AddAmigoAlertServices(builder.Configuration);

			using IHost host = builder.Build();

			Console.WriteLine("Hello, World!");

			await RunAsync(host.Services, DateTimeOffset.UtcNow);
		}

		/// <summary>
		/// One scheduled run: evaluates every due user at <paramref name="now"/>, acts on each actionable decision through the shared
		/// <see cref="EscalationService"/>, then removes old location logs. Separate from <see cref="Main"/> so the integration tests can
		/// invoke the WebJob's own entry point against a test database.
		/// </summary>
		/// <param name="services">The root provider, with the data layer and services registered.</param>
		/// <param name="now">The instant to evaluate at.</param>
		internal static async Task RunAsync(IServiceProvider services, DateTimeOffset now)
		{
			using IServiceScope scope = services.CreateScope();
			EscalationService escalationService = scope.ServiceProvider.GetRequiredService<EscalationService>();
			ICleanupService cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

			IReadOnlyList<EscalationDecision> list = await escalationService.EvaluateDueUsersAsync(now);
			foreach (EscalationDecision decision in list)
			{
				Console.WriteLine($"UserId: {decision.UserId}, Outcome: {decision.Outcome}, ScheduledFor: {decision.ScheduledFor}, EvaluatedAt: {decision.EvaluatedAt}, CheckInId: {decision.CheckInId}");
				if (decision.IsActionable)
				{
					bool acted = await escalationService.ActOnDecisionAsync(decision);
					Console.WriteLine($"Acted on decision: {acted}");
				}
			}
			int deletedLogs = await cleanupService.CleanupOldLocations();
			Console.WriteLine($"Deleted {deletedLogs} old location logs.");
		}
	}
}
