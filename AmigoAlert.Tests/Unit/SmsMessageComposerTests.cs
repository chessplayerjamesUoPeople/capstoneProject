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

			string[] all = bodies
				.Concat(SmsMessageComposer.ConsentRequestMessages(name + " Surname", url))
				.Concat(SmsMessageComposer.EmergencyActivationMessages(name, null, null, address, new string('L', 300)))
				.Concat(SmsMessageComposer.EmergencyActivationMessages(name, null, null, "Calle 1", "Near the park"))
				.ToArray();

			Assert.All(all, body => Assert.InRange(body.Length, 1, SmsMessageComposer.MaxLength));
			Assert.All(all, body => Assert.All(body, c => Assert.InRange(c, ' ', '~')));
		}

		private const string Url = "https://example.test/consent/abc";

		[Fact]
		public void ConsentRequestMessages_ShortFirstName_IsOneMessageWithTheLink()
		{
			IReadOnlyList<string> messages = SmsMessageComposer.ConsentRequestMessages("Ana Maria Lopez", Url);

			string only = Assert.Single(messages);
			Assert.Contains("Ana added", only);
			Assert.DoesNotContain("Lopez", only);
			Assert.EndsWith(Url, only);
		}

		[Fact]
		public void ConsentRequestMessages_LongFirstName_UsesFullNameAndSendsLinkSeparately()
		{
			IReadOnlyList<string> messages = SmsMessageComposer.ConsentRequestMessages("Alejandra Lopez", Url);

			Assert.Equal(2, messages.Count);
			Assert.Contains("Alejandra Lopez", messages[0]);
			Assert.Contains("second message", messages[0]);
			Assert.DoesNotContain(Url, messages[0]);
			Assert.EndsWith(Url, messages[1]);
		}

		[Fact]
		public void EmergencyActivationMessages_AddressAndLandmarksThatFit_AreOneMessage()
		{
			IReadOnlyList<string> messages = SmsMessageComposer.EmergencyActivationMessages("Ana", null, null, "Calle 1", "Near the park");

			string only = Assert.Single(messages);
			Assert.Contains("Address: Calle 1", only);
			Assert.EndsWith("Landmarks: Near the park", only);
		}

		[Fact]
		public void EmergencyActivationMessages_TooLong_SendsLandmarksSecondAndTruncatesThem()
		{
			IReadOnlyList<string> messages = SmsMessageComposer.EmergencyActivationMessages("Ana", null, null, new string('A', 100), new string('L', 400));

			Assert.Equal(2, messages.Count);
			Assert.Contains("Address: ", messages[0]);
			Assert.DoesNotContain("Landmarks", messages[0]);
			Assert.StartsWith("AmigoAlert: Landmarks: L", messages[1]);
			Assert.Equal(SmsMessageComposer.MaxLength, messages[1].Length);
		}

		[Fact]
		public void EmergencyActivationMessages_NoLandmarksOrWithLocation_IsOneMessageWithoutLandmarks()
		{
			Assert.Single(SmsMessageComposer.EmergencyActivationMessages("Ana", null, null, "Calle 1", null));

			string withLocation = Assert.Single(SmsMessageComposer.EmergencyActivationMessages("Ana", -0.18, -78.46, "Calle 1", "Near the park"));
			Assert.Contains("maps.google.com", withLocation);
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
