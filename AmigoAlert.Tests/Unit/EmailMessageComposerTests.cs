using AmigoAlert.Services;
using AmigoAlert.Shared.Enums;

namespace AmigoAlert.Tests.Unit
{
	/// <summary>
	/// The verification code email: the code is readable from the subject alone, and every body says what the code is for and when it
	/// stops working.
	/// </summary>
	[Trait("Level", "Unit")]
	public sealed class EmailMessageComposerTests
	{
		[Theory]
		[InlineData(VerificationPurposeType.Registration, "finish registering")]
		[InlineData(VerificationPurposeType.Login, "log in")]
		public void VerificationCode_CarriesTheCodePurposeAndExpiryInEveryPart(VerificationPurposeType purpose, string purposeText)
		{
			EmailContent email = EmailMessageComposer.VerificationCode("042517", purpose, TimeSpan.FromMinutes(10));

			Assert.Equal("Your AmigoAlert code is 042517", email.Subject);
			Assert.All([email.PlainText, email.Html], body =>
			{
				Assert.Contains("042517", body);
				Assert.Contains(purposeText, body);
				Assert.Contains("expires in 10 minutes", body);
			});
		}
	}
}
