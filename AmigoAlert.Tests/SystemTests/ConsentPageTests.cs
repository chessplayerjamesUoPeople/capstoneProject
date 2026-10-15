using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using AmigoAlert.Tests.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace AmigoAlert.Tests.SystemTests
{
	/// <summary>
	/// The consent page as the emergency contact uses it: the real Blazor host, its antiforgery tokens and its two forms, posted the way
	/// a browser posts them. The contact is created through the API, so the link comes from the consent SMS.
	/// </summary>
	public sealed class ConsentPageFactory : WebApplicationFactory<AmigoAlert.Web.Program>
	{
		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseSetting("ConnectionStrings:AmigoAlertDb", Environment.GetEnvironmentVariable("AMIGOALERT_TEST_SQL"));
			builder.UseSetting("Twilio:UseMockSms", "true");
			builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
		}
	}

	[Collection(IntegrationCollection.Name)]
	[Trait("Level", "System")]
	[Trait("Category", "UI")]
	public sealed class ConsentPageTests(IntegrationApiFactory api) : IAsyncLifetime, IDisposable
	{
		private const string InvalidLink = "This consent link is invalid or has expired.";

		private static readonly Regex HiddenInput = new Regex("<input[^>]*type=\"hidden\"[^>]*>", RegexOptions.Compiled);

		private static readonly Regex Attribute = new Regex("(name|value)=\"([^\"]*)\"", RegexOptions.Compiled);

		private readonly ConsentPageFactory web = new ConsentPageFactory();

		private readonly SystemTestDriver driver = new SystemTestDriver(api);

		private HttpClient browser = null!;

		public async Task InitializeAsync()
		{
			await api.ResetAsync();

			// Cookies on, so the antiforgery cookie travels with the form post as it would from a browser.
			browser = web.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
		}

		public Task DisposeAsync()
		{
			return api.RemoveRegisteredUsersAsync();
		}

		public void Dispose()
		{
			web.Dispose();
		}

		/// <summary>C-01, US-04: the link opens a page naming the user, with the terms checkbox and both buttons.</summary>
		[Fact]
		public async Task C01_OpenLink_ShowsWhoIsAskingAndBothChoices()
		{
			(UserDto user, Guid token) = await PendingContactAsync();

			string html = await browser.GetStringAsync($"consent/{token}");

			Assert.Contains($"<strong>{user.Name}</strong> has asked you to be their emergency contact", html);
			Assert.Contains("name=\"Input.IsAccepted\"", html);
			Assert.Contains(">Accept</button>", html);
			Assert.Contains(">Decline</button>", html);
		}

		/// <summary>C-02: Accept without ticking the terms records nothing.</summary>
		[Fact]
		public async Task C02_AcceptWithoutTickingTerms_RecordsNothing()
		{
			(UserDto user, Guid token) = await PendingContactAsync();

			string html = await SubmitAsync(token, "accept", tickTerms: false);

			Assert.Contains(">Accept</button>", html);
			Assert.Equal(ConsentStatusType.Pending, (await driver.GetContactAsync(user.Id)).ConsentStatus);
		}

		/// <summary>C-03, US-05: Accept with the terms ticked opts the contact in.</summary>
		[Fact]
		public async Task C03_AcceptWithTerms_OptsIn()
		{
			(UserDto user, Guid token) = await PendingContactAsync();

			string html = await SubmitAsync(token, "accept", tickTerms: true);

			Assert.Contains("Thank you. Your consent has been recorded.", html);
			Assert.Equal(ConsentStatusType.Granted, (await driver.GetContactAsync(user.Id)).ConsentStatus);
			Assert.Contains(InvalidLink, await browser.GetStringAsync($"consent/{token}"));
		}

		/// <summary>C-04, US-06: Decline opts the contact out.</summary>
		[Fact]
		public async Task C04_Decline_OptsOut()
		{
			(UserDto user, Guid token) = await PendingContactAsync();

			string html = await SubmitAsync(token, "decline", tickTerms: false);

			Assert.Contains("Your response has been recorded. You will not receive notifications.", html);
			Assert.Equal(ConsentStatusType.Rejected, (await driver.GetContactAsync(user.Id)).ConsentStatus);
		}

		/// <summary>C-05: an unknown or expired link offers no choice.</summary>
		[Fact]
		public async Task C05_UnknownOrExpiredLink_IsRefused()
		{
			Assert.Contains(InvalidLink, await browser.GetStringAsync($"consent/{Guid.NewGuid()}"));

			(UserDto user, Guid token) = await PendingContactAsync();
			await api.InNewScopeAsync(db => db.EmergencyContacts.Where(c => c.UserId == user.Id)
				.ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsentTokenExpires, DateTimeOffset.UtcNow.AddMinutes(-1))));

			string html = await browser.GetStringAsync($"consent/{token}");

			Assert.Contains(InvalidLink, html);
			Assert.DoesNotContain(">Accept</button>", html);
			Assert.Equal(ConsentStatusType.Pending, (await driver.GetContactAsync(user.Id)).ConsentStatus);
		}

		private async Task<(UserDto User, Guid Token)> PendingContactAsync()
		{
			UserDto user = await driver.RegisterAsync();
			Guid token = await driver.AddContactAsync(user.Id, new SetEmergencyContactDto("Page Contact", "+593990002222"));

			return (user, token);
		}

		/// <summary>Loads the page, then posts the named form with the hidden fields the page rendered for it.</summary>
		private async Task<string> SubmitAsync(Guid token, string formName, bool tickTerms)
		{
			string url = $"consent/{token}";
			string page = await browser.GetStringAsync(url);

			Dictionary<string, string> fields = FormFields(page, formName);
			if (tickTerms)
			{
				fields["Input.IsAccepted"] = "true";
			}

			HttpResponseMessage response = await browser.PostAsync(url, new FormUrlEncodedContent(fields));
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			return await response.Content.ReadAsStringAsync();
		}

		/// <summary>The hidden inputs of the form whose <c>_handler</c> is <paramref name="formName"/>.</summary>
		private static Dictionary<string, string> FormFields(string page, string formName)
		{
			foreach (Match form in Regex.Matches(page, "<form[^>]*>(.*?)</form>", RegexOptions.Singleline))
			{
				Dictionary<string, string> fields = HiddenInput.Matches(form.Value)
					.Select(input => Attribute.Matches(input.Value).ToDictionary(a => a.Groups[1].Value, a => WebUtility.HtmlDecode(a.Groups[2].Value)))
					.Where(a => a.ContainsKey("name"))
					.ToDictionary(a => a["name"], a => a.GetValueOrDefault("value", string.Empty));

				if (fields.GetValueOrDefault("_handler") == formName)
				{
					return fields;
				}
			}

			throw new Xunit.Sdk.XunitException($"The page has no form named '{formName}'.");
		}
	}
}
