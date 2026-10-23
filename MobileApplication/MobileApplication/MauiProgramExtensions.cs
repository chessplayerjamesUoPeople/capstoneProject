using System.Net.Security;
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

			builder.Services.AddTransient<TransientRetryHandler>();
			builder.Services.AddHttpClient<AmigoAlertApiClient>(client => client.BaseAddress = new Uri(ApiSettings.BaseAddress))
				.ConfigurePrimaryHttpMessageHandler(CreateHttpMessageHandler)
				.AddHttpMessageHandler<TransientRetryHandler>();

			// Shell resolves registered pages from the container, so they can take the API client in their constructors.
			builder.Services.AddTransient<HomePage>();
			builder.Services.AddTransient<RegistrationPage>();
			builder.Services.AddTransient<TermsAndConditionsPage>();
			builder.Services.AddTransient<HelpPage>();

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder;
		}

		private static HttpMessageHandler CreateHttpMessageHandler()
		{
			HttpClientHandler handler = new HttpClientHandler();

#if DEBUG
			// The ASP.NET Core development certificate is not trusted by the emulator, and is issued for localhost rather than 10.0.2.2 or
			// the PC's LAN address. Accept it for the local API only; every other host, including the deployed API, is still validated
			// normally. The LAN host is read from ApiSettings, so changing the address there is enough.
			var localApiHost = new Uri(ApiSettings.LocalBaseAddress).Host;
			handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
				errors == SslPolicyErrors.None
				|| request.RequestUri is { Host: "10.0.2.2" or "localhost" }
				|| request.RequestUri?.Host == localApiHost;
#endif

			return handler;
		}
	}
}
