using AmigoAlert.Data;

namespace AmigoAlert_API
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.

			builder.Services.AddControllers();



			builder.Services.AddAmigoAlertData(
				builder.Configuration.GetConnectionString("AmigoAlertDb")!);

			var app = builder.Build();

			// Configure the HTTP request pipeline.

			app.UseHttpsRedirection();

			app.UseAuthorization();


			app.MapControllers();

			app.Run();
		}
	}
}
