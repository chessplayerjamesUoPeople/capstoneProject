using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-07 to I-10: answering a check-in through the API. Check-ins are created by the WebJob, never by the device, so each test starts
	/// from the state the WebJob leaves after notifying: the check-in row and the matching pending status. The device answers by check-in
	/// id (PUT /api/checkins/{id}/response); there is no user-keyed submit.
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public sealed class CheckInTests(IntegrationApiFactory factory) : IAsyncLifetime
	{
		/// <summary>Seed.sql's first user.</summary>
		private static readonly Guid UserId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

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
		public async Task I07_SubmitCheckIn_SetsOkayAndRecordsTime()
		{
			long checkInId = await NotifyAsync(withReminder: false);
			DateTimeOffset before = DateTimeOffset.UtcNow;

			HttpResponseMessage response = await AnswerAsync(checkInId);

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			Assert.Equal((byte)StatusType.Okay, await factory.InNewScopeAsync(db => db.Users.Where(u => u.Id == UserId)
				.Select(u => u.StatusId).SingleAsync()));

			CheckIn checkIn = await factory.InNewScopeAsync(db => db.CheckIns.SingleAsync(c => c.Id == checkInId));
			Assert.Equal((byte)CheckInResponseType.Okay, checkIn.ResponseId);
			Assert.InRange(checkIn.TimeReceived!.Value, before, DateTimeOffset.UtcNow);

			StatusLog log = await factory.InNewScopeAsync(db => db.StatusLogs.SingleAsync(l => l.UserId == UserId));
			Assert.Equal((byte)StatusType.Okay, log.StatusId);
			Assert.Equal(checkIn.TimeReceived, log.TimeOccurrence);
		}

		[Fact]
		public async Task I08_SubmitCheckInWhilePending_ClearsToOkayWithoutSms()
		{
			// Pending2: the reminder has gone out too, so the next poll would escalate.
			long reminderId = await NotifyAsync(withReminder: true);

			HttpResponseMessage response = await AnswerAsync(reminderId);

			Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
			Assert.Equal((byte)StatusType.Okay, await factory.InNewScopeAsync(db => db.Users.Where(u => u.Id == UserId)
				.Select(u => u.StatusId).SingleAsync()));
			Assert.NotNull(await factory.InNewScopeAsync(db => db.CheckIns.Where(c => c.Id == reminderId)
				.Select(c => c.TimeReceived).SingleAsync()));

			Assert.Empty(factory.SentSms);
			Assert.False(await factory.InNewScopeAsync(db => db.Sms.AnyAsync(m => m.EmergencyContact.UserId == UserId)));
		}

		[Fact]
		public async Task I09_SubmitCheckInForUnknownTarget_Returns404AndChangesNothing()
		{
			int checkInsBefore = await factory.InNewScopeAsync(db => db.CheckIns.CountAsync());
			int logsBefore = await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync());

			// The device answers by check-in id, and lists history by user id; both unknown.
			HttpResponseMessage answer = await AnswerAsync(long.MaxValue);
			HttpResponseMessage history = await client.GetAsync($"api/users/{Guid.NewGuid()}/checkins");

			Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
			Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
			Assert.Equal(checkInsBefore, await factory.InNewScopeAsync(db => db.CheckIns.CountAsync()));
			Assert.Equal(logsBefore, await factory.InNewScopeAsync(db => db.StatusLogs.CountAsync()));
			Assert.Empty(factory.SentSms);
		}

		[Fact]
		public async Task I10_GetHistoryAfterSubmitting_IncludesTheCheckIn()
		{
			long checkInId = await NotifyAsync(withReminder: false);
			Assert.Equal(HttpStatusCode.NoContent, (await AnswerAsync(checkInId)).StatusCode);

			HttpResponseMessage response = await client.GetAsync($"api/users/{UserId}/checkins");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>();
			JsonElement item = Assert.Single(json.EnumerateArray());
			IntegrationApiFactory.AssertShape<CheckInDto>(item);

			CheckInDto dto = item.Deserialize<CheckInDto>(JsonSerializerOptions.Web)!;
			CheckIn stored = await factory.InNewScopeAsync(db => db.CheckIns.SingleAsync(c => c.Id == checkInId));
			Assert.Equal(stored.Id, dto.Id);
			Assert.Equal(stored.ScheduledFor, dto.ScheduledFor);
			Assert.Equal(stored.TimeSent, dto.TimeSent);
			Assert.Equal(stored.TimeReceived, dto.TimeReceived);
			Assert.Equal(CheckInResponseType.Okay, dto.Response);
			Assert.Equal(stored.TimeDelivered, dto.TimeDelivered);
		}

		private Task<HttpResponseMessage> AnswerAsync(long checkInId)
		{
			return client.PutAsJsonAsync($"api/checkins/{checkInId}/response", new RespondToCheckInDto(CheckInResponseType.Okay));
		}

		/// <summary>
		/// What the WebJob leaves behind after notifying the user of an occurrence 20 minutes ago: the first notification, and the reminder
		/// if <paramref name="withReminder"/>, with the user at Pending1 or Pending2 to match.
		/// </summary>
		/// <returns>The id of the newest check-in, the one the device would answer.</returns>
		private async Task<long> NotifyAsync(bool withReminder)
		{
			DateTimeOffset now = DateTimeOffset.UtcNow;
			DateTimeOffset scheduledFor = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMinute, TimeSpan.Zero).AddMinutes(-20);

			List<CheckIn> checkIns = [NewCheckIn(scheduledFor, scheduledFor)];
			if (withReminder)
			{
				checkIns.Add(NewCheckIn(scheduledFor, scheduledFor.AddMinutes(10)));
			}

			StatusType status = withReminder ? StatusType.Pending2 : StatusType.Pending1;

			return await factory.InNewScopeAsync(async db =>
			{
				db.CheckIns.AddRange(checkIns);
				await db.SaveChangesAsync();
				await db.Users.Where(u => u.Id == UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.StatusId, (byte)status));

				return checkIns[^1].Id;
			});
		}

		private static CheckIn NewCheckIn(DateTimeOffset scheduledFor, DateTimeOffset sentAt)
		{
			return new CheckIn
			{
				UserId = UserId,
				ScheduledFor = scheduledFor,
				TimeSent = sentAt,
				ResponseId = (byte)CheckInResponseType.NoResponse
			};
		}
	}
}
