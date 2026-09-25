using AmigoAlert.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AmigoAlert.Data
{
	public static class DependencyInjection
	{
		public static IServiceCollection AddAmigoAlertData(
			this IServiceCollection services, string connectionString)
		{
			services.AddDbContext<AmigoAlertDbContext>(options => options
				.UseSqlServer(connectionString, sqlOptions => sqlOptions.UseNetTopologySuite())
					.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

			services.AddDbContext<AmigoAlertDbContext>(options => options
				.UseSqlServer(connectionString)
				.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

			services.AddScoped<IUserRepository, UserRepository>();
			services.AddScoped<ICheckInRepository, CheckInRepository>();
			services.AddScoped<ISmsRepository, SmsRepository>();

			return services;
		}
	}
}
