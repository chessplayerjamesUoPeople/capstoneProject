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
		/// Registers the escalation, consent, registration, log in, SMS and email services, binding <see cref="TwilioOptions"/>,
		/// <see cref="ConsentOptions"/> and <see cref="EmailOptions"/> from <paramref name="configuration"/>. <see cref="MockSmsService"/>
		/// replaces <see cref="SmsService"/> when <c>Twilio:UseMockSms</c> is true, and <see cref="MockEmailService"/> replaces
		/// <see cref="EmailService"/> when <c>Email:UseMockEmail</c> is true.
		/// </summary>
		/// <param name="services">The service collection to add to.</param>
		/// <param name="configuration">The host configuration containing the <c>Twilio</c> and <c>Consent</c> sections.</param>
		/// <returns>The same service collection, for chaining.</returns>
		public static IServiceCollection AddAmigoAlertServices(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
			services.Configure<ConsentOptions>(configuration.GetSection(ConsentOptions.SectionName));
			services.Configure<NotificationHubOptions>(configuration.GetSection(NotificationHubOptions.SectionName));
			services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

			services.TryAddSingleton(TimeProvider.System);

			// With the mock on, no Twilio client is registered at all, so nothing in the process can reach Twilio.
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

			// With the mock on, nothing in the process can reach Communication Services.
			if (configuration.GetSection(EmailOptions.SectionName).GetValue<bool>(nameof(EmailOptions.UseMockEmail)))
			{
				services.AddSingleton<IEmailService, MockEmailService>();
			}
			else
			{
				services.AddSingleton<IEmailService, EmailService>();
			}

			services.AddSingleton<IPushNotificationService, NotificationHubPushService>();

			services.AddScoped<EscalationService>();
			services.AddScoped<ICleanupService, CleanupService>();
			services.AddScoped<ConsentService>();
			services.AddScoped<EmailVerificationService>();
			services.AddScoped<RegistrationService>();
			services.AddScoped<LoginService>();

			return services;
		}
	}
}
