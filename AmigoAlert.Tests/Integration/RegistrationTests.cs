using AmigoAlert.Data.Entities;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-01 to I-06: registration and profile updates through the API, read back from SQL Server. Registration is two calls: POST
	/// /api/users creates the user and their initial status log row, and POST /api/users/{id}/emergency-contact adds the contact at pending
	/// consent and sends the consent request SMS. Consent is a state of the contact row, not a table of its own.
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public sealed class RegistrationTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
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
		public async Task I01_RegisterWithEmergencyContact_PersistsUserContactAndConsentRequest()
		{
			CreateUserDto request = NewUser();
			HttpResponseMessage registered = await client.PostAsJsonAsync("api/users", request);
			Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
			UserDto user = (await registered.Content.ReadFromJsonAsync<UserDto>())!;

			SetEmergencyContactDto contactRequest = new SetEmergencyContactDto("Integration Contact", "+593990009999");
			HttpResponseMessage added = await client.PostAsJsonAsync($"api/users/{user.Id}/emergency-contact", contactRequest);
			Assert.Equal(HttpStatusCode.Created, added.StatusCode);
			EmergencyContactDto contact = (await added.Content.ReadFromJsonAsync<EmergencyContactDto>())!;

			Assert.NotEqual(Guid.Empty, user.Id);
			Assert.NotEqual(Guid.Empty, contact.Id);

			User storedUser = await factory.InNewScopeAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));
			Assert.Equal(request.Email, storedUser.Email);
			Assert.Equal((byte)StatusType.Okay, storedUser.StatusId);
			Assert.Equal(request.AcceptedTermsVersion, storedUser.AcceptedTermsVersion);
			Assert.Equal(user.AcceptedTerms, storedUser.AcceptedTermsTime);

			StatusLog log = await factory.InNewScopeAsync(db => db.StatusLogs.SingleAsync(l => l.UserId == user.Id));
			Assert.Equal((byte)StatusType.Okay, log.StatusId);

			EmergencyContact storedContact = await factory.InNewScopeAsync(db => db.EmergencyContacts.SingleAsync(c => c.UserId == user.Id));
			Assert.Equal(contact.Id, storedContact.Id);
			Assert.Equal(contactRequest.Phone, storedContact.Phone);
			Assert.Equal((byte)ConsentStatusType.Pending, storedContact.ConsentStatusId);
			Assert.True(storedContact.IsActive);
			Assert.NotNull(storedContact.ConsentToken);
			Assert.Equal(storedContact.ConsentRequestedAt + ConsentService.TokenLifetime, storedContact.ConsentTokenExpires);
			Assert.Null(storedContact.ConsentRespondedAt);

			// The registered name's first name is longer than five characters, so the request is two SMS: the notice, then the link.
			List<Sms> smsRows = await factory.InNewScopeAsync(db => db.Sms.Where(m => m.EmergencyContactId == contact.Id).ToListAsync());
			Assert.Equal(2, smsRows.Count);
			Assert.All(smsRows, sms => Assert.Equal((byte)SmsMessageTypeEnum.ConsentRequest, sms.MessageTypeId));

			Assert.Equal(2, factory.SentSms.Count);
			Assert.All(factory.SentSms, sent => Assert.Equal(contactRequest.Phone, sent.ToPhone));
			Assert.Contains(storedContact.ConsentToken.Value.ToString("N"), factory.SentSms.Last().Body);
		}

		[Fact]
		public async Task I02_RegisterThenGet_ReturnsWhatWasPosted()
		{
			CreateUserDto request = NewUser();
			HttpResponseMessage registered = await client.PostAsJsonAsync("api/users", request);
			Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
			UserDto created = (await registered.Content.ReadFromJsonAsync<UserDto>())!;

			// The Location header, so CreatedAtAction's route is covered too.
			HttpResponseMessage response = await client.GetAsync(registered.Headers.Location);
			Assert.Equal(HttpStatusCode.OK, response.StatusCode);

			JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
			IntegrationApiFactory.AssertShape<UserDto>(json);
			UserDto user = json.Deserialize<UserDto>(JsonSerializerOptions.Web)!;

			Assert.Equal(created.Id, user.Id);
			Assert.Equal(request.Name, user.Name);
			Assert.Equal(request.Email, user.Email);
			Assert.Equal(request.Phone, user.Phone);
			Assert.Equal(request.Address, user.Address);
			Assert.Equal(request.Landmarks, user.Landmarks);
			Assert.Equal(request.Notification1Time, user.Notification1Time);
			Assert.Equal(request.Notification2Time, user.Notification2Time);
			Assert.Equal(StatusType.Okay, user.Status);
			Assert.Equal(created.AcceptedTerms, user.AcceptedTerms);
		}

		[Fact]
		public async Task I03_RegisterWithDuplicateEmail_IsRejectedAndWritesNothing()
		{
			CreateUserDto first = NewUser();
			Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", first)).StatusCode);
			int usersBefore = await factory.InNewScopeAsync(db => db.Users.CountAsync());
			int logsBefore = await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync());

			CreateUserDto duplicate = first with { Name = "Someone Else", Address = "99 Other St., Otavalo, Ecuador" };
			HttpResponseMessage response = await client.PostAsJsonAsync("api/users", duplicate);

			Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
			Assert.Equal(usersBefore, await factory.InNewScopeAsync(db => db.Users.CountAsync()));
			Assert.Equal(logsBefore, await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync()));

			User user = Assert.Single(await factory.InNewScopeAsync(db => db.Users.Where(u => u.Email == first.Email).ToListAsync()));
			Assert.Equal(first.Name, user.Name);
			Assert.Equal(first.Address, user.Address);
		}

		[Fact]
		public async Task I04_RegisterWithRequiredFieldMissing_Returns400AndWritesNothing()
		{
			string email = NewEmail();
			int usersBefore = await factory.InNewScopeAsync(db => db.Users.CountAsync());
			int logsBefore = await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync());

			// No address.
			HttpResponseMessage response = await client.PostAsJsonAsync("api/users",
				new { name = "Integration User", email, acceptedTermsVersion = "1.0" });

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
			Assert.Contains(nameof(CreateUserDto.Address), problem.Errors.Keys);

			Assert.False(await factory.InNewScopeAsync(db => db.Users.AnyAsync(u => u.Email == email)));
			Assert.Equal(usersBefore, await factory.InNewScopeAsync(db => db.Users.CountAsync()));
			Assert.Equal(logsBefore, await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync()));
		}

		[Fact]
		public async Task I05_UpdateUser_PersistsProfileAndLeavesEverythingElse()
		{
			UserDto created = await RegisterAsync(client, NewUser());
			User before = await factory.InNewScopeAsync(db => db.Users.SingleAsync(u => u.Id == created.Id));

			// Email is not part of UpdateUserDto; sent anyway, to prove it is ignored rather than bound.
			HttpResponseMessage response = await client.PutAsJsonAsync($"api/users/{created.Id}", new
			{
				name = "Integration User Renamed",
				email = NewEmail(),
				phone = "+593980008888",
				address = "10 Other St., Otavalo, Ecuador",
				landmarks = (string?)null
			});
			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

			User after = await factory.InNewScopeAsync(db => db.Users.SingleAsync(u => u.Id == created.Id));
			Assert.Equal("Integration User Renamed", after.Name);
			Assert.Equal("+593980008888", after.Phone);
			Assert.Equal("10 Other St., Otavalo, Ecuador", after.Address);
			Assert.Null(after.Landmarks);

			Assert.Equal(before.Email, after.Email);
			Assert.Equal(before.StatusId, after.StatusId);
			Assert.Equal(before.AcceptedTermsTime, after.AcceptedTermsTime);
			Assert.Equal(before.AcceptedTermsVersion, after.AcceptedTermsVersion);
			Assert.Equal(before.Notification1Time, after.Notification1Time);
			Assert.Equal(before.Notification2Time, after.Notification2Time);
		}

		[Fact]
		public async Task I06_UpdateNonExistentUser_Returns404AndCreatesNothing()
		{
			Guid id = Guid.NewGuid();
			int usersBefore = await factory.InNewScopeAsync(db => db.Users.CountAsync());

			HttpResponseMessage response = await client.PutAsJsonAsync($"api/users/{id}",
				new UpdateUserDto("Nobody", null, "0 Nowhere St., Cotacachi, Ecuador", null));

			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
			Assert.False(await factory.InNewScopeAsync(db => db.Users.AnyAsync(u => u.Id == id)));
			Assert.Equal(usersBefore, await factory.InNewScopeAsync(db => db.Users.CountAsync()));
		}

		/// <summary>A valid registration request with a unique email in <see cref="IntegrationApiFactory.RegisteredEmailDomain"/>.</summary>
		internal static CreateUserDto NewUser()
		{
			return new CreateUserDto("Integration User", NewEmail(), "+593980009999", "9 Test St., Cotacachi, Ecuador", "Blue door",
				new TimeOnly(9, 0), new TimeOnly(19, 0), "1.0");
		}

		internal static async Task<UserDto> RegisterAsync(HttpClient client, CreateUserDto request)
		{
			HttpResponseMessage response = await client.PostAsJsonAsync("api/users", request);
			Assert.Equal(HttpStatusCode.Created, response.StatusCode);

			return (await response.Content.ReadFromJsonAsync<UserDto>())!;
		}

		private static string NewEmail()
		{
			return $"{Guid.NewGuid():N}{IntegrationApiFactory.RegisteredEmailDomain}";
		}
	}
}
