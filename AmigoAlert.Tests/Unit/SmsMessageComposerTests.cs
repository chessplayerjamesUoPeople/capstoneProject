using AmigoAlert.Services;
using AmigoAlert.Services.Rules;

namespace AmigoAlert.Tests.Unit
{
	/// <summary>
	/// NF-13, as its verification asks: every body the composer can produce fits one 160-character segment, whatever the length of the
	/// name, address or URL put into it.
	/// </summary>
	[Trait("Level", "Unit")]
	public sealed class SmsMessageComposerTests
	{
		private static readonly DateTimeOffset Scheduled = new DateTimeOffset(2026, 10, 1, 8, 0, 0, EscalationRules.LocalOffset);

		public static TheoryData<int> NameLengths => new TheoryData<int> { 1, 20, 50, 160, 500 };

		[Theory]
		[MemberData(nameof(NameLengths))]
		public void EveryMessage_FitsOneSegment(int nameLength)
		{
			string name = new string('N', nameLength);
			string address = new string('A', 200);
			string url = "https://amigoalert-hqd4hwcrduhvffba.westus-01.azurewebsites.net/consent/" + Guid.NewGuid().ToString("N");

			string[] bodies =
			[
				SmsMessageComposer.ConsentRequest(name, url),
				SmsMessageComposer.MissedCheckIn(name, Scheduled),
				SmsMessageComposer.MissedCheckIn(name, Scheduled, -0.180653, -78.467838, Scheduled.AddMinutes(1)),
				SmsMessageComposer.EmergencyActivation(name, null, null, address),
				SmsMessageComposer.EmergencyActivation(name, -0.180653, -78.467838, address),
				SmsMessageComposer.EmergencyCancelled(name)
			];

			Assert.All(bodies, body => Assert.InRange(body.Length, 1, SmsMessageComposer.MaxLength));
			Assert.All(bodies, body => Assert.All(body, c => Assert.InRange(c, ' ', '~')));
		}

		[Fact]
		public void ConsentRequest_KeepsTheWholeLinkWhenTheNameIsTruncated()
		{
			string url = "https://example.test/consent/" + Guid.NewGuid().ToString("N");

			string body = SmsMessageComposer.ConsentRequest(new string('N', 500), url);

			Assert.EndsWith(url, body);
		}

		[Fact]
		public void MissedCheckIn_ShowsTheOccurrenceInEcuadorTime()
		{
			string body = SmsMessageComposer.MissedCheckIn("Ana", Scheduled.ToUniversalTime());

			Assert.Contains("08:00", body);
		}
	}
}
