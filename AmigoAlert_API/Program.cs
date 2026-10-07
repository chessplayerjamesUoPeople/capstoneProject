using AmigoAlert.Data;
using AmigoAlert.Services;

namespace AmigoAlert_API
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

				// Pulls the Twilio credentials from Key Vault, over the empty placeholders in appsettings.json.
				builder.Configuration.AddAmigoAlertKeyVault();

			// Add services to the container.

			// Keeps the Async suffix in action names, so CreatedAtAction(nameof(GetAsync), ...) resolves the GET action.
			builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false);



			builder.Services.AddAmigoAlertData(builder.Configuration.GetConnectionString("AmigoAlertDb")!);
			builder.Services.AddAmigoAlertServices(builder.Configuration);

			var app = builder.Build();

			// Configure the HTTP request pipeline.

			app.UseHttpsRedirection();

			app.UseAuthorization();


			app.MapControllers();

			app.Run();
		}
	}
}
