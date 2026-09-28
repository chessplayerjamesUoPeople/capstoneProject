using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Twilio.Clients;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Registers the service layer. Call after <c>AddAmigoAlertData</c>, which provides the repositories these services depend on.
	/// </summary>
	public static class DependencyInjection
	{
		/// <summary>
		/// Registers the escalation, consent, registration and SMS services, binding <see cref="TwilioOptions"/> and
		/// <see cref="ConsentOptions"/> from <paramref name="configuration"/>. <c>Twilio:UseMockSms</c> selects <see cref="MockSmsService"/>
		/// in place of the Twilio-backed <see cref="SmsService"/>.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="configuration">The host configuration containing the <c>Twilio</c> and <c>Consent</c> sections.</param>
		/// <returns>The same service collection, for chaining.</returns>
		public static IServiceCollection AddAmigoAlertServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
			services.Configure<ConsentOptions>(configuration.GetSection(ConsentOptions.SectionName));

			services.TryAddSingleton(TimeProvider.System);

			if (configuration.GetSection(TwilioOptions.SectionName).GetValue<bool>(nameof(TwilioOptions.UseMockSms)))
			{
				services.AddScoped<ISmsService, MockSmsService>();
			}
			else
			{
				// One client for the process: each TwilioRestClient owns an HttpClient, so creating one per scope would exhaust sockets.
				services.AddSingleton<ITwilioRestClient>(provider =>
				{
					TwilioOptions settings = provider.GetRequiredService<IOptions<TwilioOptions>>().Value;
					return new TwilioRestClient(settings.AccountSid, settings.AuthToken);
				});

				services.AddScoped<ISmsService, SmsService>();
			}

			services.AddScoped<EscalationService>();
			services.AddScoped<ConsentService>();
			services.AddScoped<RegistrationService>();

			return services;
		}
	}
}
