using AmigoAlert.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AmigoAlert.Data
{
	/// <summary>
	/// Registers the data layer. Repository implementations are internal, so host projects only ever see the interfaces.
	/// </summary>
	public static class DependencyInjection
	{
		/// <summary>
		/// Registers <see cref="AmigoAlertDbContext"/> with no-tracking queries by default, the repositories and the transaction provider.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="connectionString">The Azure SQL connection string.</param>
		/// <returns>The same service collection, for chaining.</returns>
		public static IServiceCollection AddAmigoAlertData(this IServiceCollection services, string connectionString)
		{
			services.AddDbContext<AmigoAlertDbContext>(options => options
				.UseSqlServer(connectionString, sqlOptions => sqlOptions.UseNetTopologySuite())
				.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

			services.AddScoped<ITransactionProvider, TransactionProvider>();
			services.AddScoped<IUserRepository, UserRepository>();
			services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();
			services.AddScoped<ICheckInRepository, CheckInRepository>();
			services.AddScoped<ISmsRepository, SmsRepository>();
			services.AddScoped<IStatusLogRepository, StatusLogRepository>();

			return services;
		}
	}
}
