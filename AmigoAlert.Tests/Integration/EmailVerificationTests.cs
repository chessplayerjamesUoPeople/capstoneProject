using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-20 to I-28: the emailed one-time codes that confirm an address at registration and stand in for a password at log in, through the
	/// API and read back from SQL Server. Email goes to <see cref="MockEmailService"/>, and the code is read from it as the user would read
	/// it from their inbox.
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public sealed class EmailVerificationTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
		private const string InvalidCodeDetail = "The verification code is incorrect or has expired.";

		private readonly HttpClient client = factory.CreateClient();

		public Task InitializeAsync()
		{
			return factory.ResetAsync();
		}

		public Task DisposeAsync()
		{
			return factory.RemoveRegisteredUsersAsync();
		}

		[Fact]
		public async Task I20_RequestRegistrationCode_EmailsTheCodeAndStoresOnlyItsHash()
		{
			string email = RegistrationTests.NewEmail();

			HttpResponseMessage response = await RequestCodeAsync(email, VerificationPurposeType.Registration);

			Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
			MockEmailService.SentMessage sent = Assert.Single(factory.SentEmail);
			Assert.Equal(email, sent.ToEmail);
			string code = factory.LatestCode(email);
			Assert.Matches(VerificationCodeFormat.Pattern, code);
			Assert.Contains(code, sent.Content.PlainText);
			Assert.Contains(code, sent.Content.Html);

			EmailVerification stored = await factory.InNewScopeAsync(db => db.EmailVerifications.SingleAsync(v => v.Email == email));
			Assert.Equal((byte)VerificationPurposeType.Registration, stored.PurposeId);
			Assert.Equal(stored.CreatedAt + EmailVerificationService.CodeLifetime, stored.ExpiresAt);
			Assert.Equal(0, stored.FailedAttempts);
			Assert.Null(stored.ConsumedAt);
			Assert.Equal(32, stored.CodeHash.Length);
			Assert.NotEqual(SHA256.HashData(Encoding.UTF8.GetBytes(code)), stored.CodeHash);
		}

		[Fact]
		public async Task I21_RegisterWithWrongOrMissingCode_Returns400WritesNothingAndKeepsTheCodeUsable()
		{
			CreateUserDto request = await RegistrationTests.WithCodeAsync(factory, client, RegistrationTests.NewUser());
			int usersBefore = await factory.InNewScopeAsync(db => db.Users.CountAsync());

			HttpResponseMessage wrong = await client.PostAsJsonAsync("api/users", request with { VerificationCode = OtherCode(request.VerificationCode) });
			Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
			Assert.Equal(InvalidCodeDetail, (await wrong.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);

			HttpResponseMessage missing = await client.PostAsJsonAsync("api/users", new
			{
				request.Name, request.Email, request.Address, request.AcceptedTermsVersion
			});
			Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
			Assert.Contains(nameof(CreateUserDto.VerificationCode),
				(await missing.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);

			Assert.Equal(usersBefore, await factory.InNewScopeAsync(db => db.Users.CountAsync()));
			EmailVerification stored = await factory.InNewScopeAsync(db => db.EmailVerifications.SingleAsync(v => v.Email == request.Email));
			Assert.Equal(1, stored.FailedAttempts);

			// One wrong guess does not cost the user their code.
			Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", request)).StatusCode);
			Assert.NotNull(await factory.InNewScopeAsync(db => db.EmailVerifications.Where(v => v.Email == request.Email)
				.Select(v => v.ConsumedAt).SingleAsync()));
		}

		[Fact]
		public async Task I22_RegisterWithExpiredCode_Returns400AndWritesNothing()
		{
			CreateUserDto request = await RegistrationTests.WithCodeAsync(factory, client, RegistrationTests.NewUser());
			await factory.InNewScopeAsync(db => db.EmailVerifications.Where(v => v.Email == request.Email)
				.ExecuteUpdateAsync(s => s.SetProperty(v => v.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1))));

			HttpResponseMessage response = await client.PostAsJsonAsync("api/users", request);

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.False(await factory.InNewScopeAsync(db => db.Users.AnyAsync(u => u.Email == request.Email)));
		}

		[Fact]
		public async Task I23_RequestNewCode_SupersedesThePreviousOne()
		{
			CreateUserDto first = await RegistrationTests.WithCodeAsync(factory, client, RegistrationTests.NewUser());
			await PastCooldownAsync(first.Email);
			CreateUserDto second = await RegistrationTests.WithCodeAsync(factory, client, first);

			Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("api/users", first)).StatusCode);
			Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", second)).StatusCode);
		}

		[Fact]
		public async Task I24_RequestCodeForTheWrongKindOfAddress_IsRefusedAndSendsNothing()
		{
			UserDto registered = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());
			factory.Email.Clear();

			HttpResponseMessage registerAgain = await RequestCodeAsync(registered.Email, VerificationPurposeType.Registration);
			HttpResponseMessage unknownLogin = await RequestCodeAsync(RegistrationTests.NewEmail(), VerificationPurposeType.Login);

			Assert.Equal(HttpStatusCode.Conflict, registerAgain.StatusCode);
			Assert.Equal(HttpStatusCode.NotFound, unknownLogin.StatusCode);
			Assert.Empty(factory.SentEmail);
		}

		[Fact]
		public async Task I25_RequestCodesTooOften_Returns429WithRetryAfterAndSendsNothing()
		{
			string email = RegistrationTests.NewEmail();
			Assert.Equal(HttpStatusCode.Accepted, (await RequestCodeAsync(email, VerificationPurposeType.Registration)).StatusCode);

			// Within the resend cooldown.
			HttpResponseMessage tooSoon = await RequestCodeAsync(email, VerificationPurposeType.Registration);
			Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
			Assert.InRange(tooSoon.Headers.RetryAfter!.Delta!.Value, TimeSpan.FromSeconds(1), EmailVerificationService.ResendCooldown);

			// Past the cooldown, but at the hourly limit.
			for (int i = 1; i < EmailVerificationService.MaxCodesPerHour; i++)
			{
				await PastCooldownAsync(email);
				Assert.Equal(HttpStatusCode.Accepted, (await RequestCodeAsync(email, VerificationPurposeType.Registration)).StatusCode);
			}

			await PastCooldownAsync(email);
			HttpResponseMessage overLimit = await RequestCodeAsync(email, VerificationPurposeType.Registration);
			Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
			Assert.NotNull(overLimit.Headers.RetryAfter);

			Assert.Equal(EmailVerificationService.MaxCodesPerHour, factory.SentEmail.Count);
		}

		[Fact]
		public async Task I26_LogInWithValidCode_ReturnsTheRegisteredUserOnce()
		{
			UserDto registered = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());

			// Addresses are matched without regard to case, as the user may type them differently on a new device.
			string typed = registered.Email.ToUpperInvariant();
			Assert.Equal(HttpStatusCode.Accepted, (await RequestCodeAsync(typed, VerificationPurposeType.Login)).StatusCode);
			LogInDto request = new LogInDto(typed, factory.LatestCode(typed));

			HttpResponseMessage response = await client.PostAsJsonAsync("api/users/login", request);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
			IntegrationApiFactory.AssertShape<UserDto>(json);
			Assert.Equal(registered.Id, json.Deserialize<UserDto>(JsonSerializerOptions.Web)!.Id);

			HttpResponseMessage reused = await client.PostAsJsonAsync("api/users/login", request);
			Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
			Assert.Equal(InvalidCodeDetail, (await reused.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
		}

		[Fact]
		public async Task I27_LogInAfterTooManyWrongCodes_RejectsEvenTheRightOne()
		{
			UserDto registered = await RegistrationTests.RegisterAsync(factory, client, RegistrationTests.NewUser());
			Assert.Equal(HttpStatusCode.Accepted, (await RequestCodeAsync(registered.Email, VerificationPurposeType.Login)).StatusCode);
			string code = factory.LatestCode(registered.Email);

			for (int i = 0; i < EmailVerificationService.MaxFailedAttempts; i++)
			{
				Assert.Equal(HttpStatusCode.BadRequest,
					(await client.PostAsJsonAsync("api/users/login", new LogInDto(registered.Email, OtherCode(code)))).StatusCode);
			}

			Assert.Equal(HttpStatusCode.BadRequest,
				(await client.PostAsJsonAsync("api/users/login", new LogInDto(registered.Email, code))).StatusCode);
		}

		[Fact]
		public async Task I28_CodeForOnePurpose_DoesNotWorkForTheOther()
		{
			CreateUserDto request = await RegistrationTests.WithCodeAsync(factory, client, RegistrationTests.NewUser());
			Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", request)).StatusCode);

			// The registration code was used, and was never a log in code.
			HttpResponseMessage response = await client.PostAsJsonAsync("api/users/login",
				new LogInDto(request.Email, request.VerificationCode));

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		}

		private Task<HttpResponseMessage> RequestCodeAsync(string email, VerificationPurposeType purpose)
		{
			return client.PostAsJsonAsync("api/verification-codes", new RequestVerificationCodeDto(email, purpose));
		}

		/// <summary>Moves every code already sent to <paramref name="email"/> back past the resend cooldown, keeping them inside the hour.</summary>
		private Task<int> PastCooldownAsync(string email)
		{
			string address = email.ToLowerInvariant();
			double seconds = -(EmailVerificationService.ResendCooldown.TotalSeconds + 1);

			return factory.InNewScopeAsync(db => db.EmailVerifications.Where(v => v.Email == address)
				.ExecuteUpdateAsync(s => s.SetProperty(v => v.CreatedAt, v => v.CreatedAt.AddSeconds(seconds))));
		}

		/// <summary>A well-formed code that is not <paramref name="code"/>.</summary>
		private static string OtherCode(string code)
		{
			return code == "123456" ? "654321" : "123456";
		}
	}
}
