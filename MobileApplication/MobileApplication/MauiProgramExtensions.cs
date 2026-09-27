using Microsoft.Extensions.Logging;
using MobileApplication.Pages;
using MobileApplication.Services;

namespace MobileApplication
{
	public static class MauiProgramExtensions
	{
		public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder)
		{
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			builder.Services.AddHttpClient<AmigoAlertApiClient>(client => client.BaseAddress = new Uri(ApiSettings.BaseAddress));

			// Shell resolves registered pages from the container, so they can take the API client in their constructors.
			builder.Services.AddTransient<HomePage>();
			builder.Services.AddTransient<RegistrationPage>();

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder;
		}
	}
}
