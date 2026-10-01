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

			builder.Services.AddAmigoAlertData(builder.Configuration.GetConnectionString("AmigoAlertDb")!);
			builder.Services.AddAmigoAlertServices(builder.Configuration);

			using IHost host = builder.Build();

			using IServiceScope scope = host.Services.CreateScope();
			EscalationService escalationService = scope.ServiceProvider.GetRequiredService<EscalationService>();
			ICleanupService cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();

			Console.WriteLine("Hello, World!");

			IReadOnlyList<EscalationDecision> list = await escalationService.EvaluateDueUsersAsync(DateTimeOffset.UtcNow);
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
