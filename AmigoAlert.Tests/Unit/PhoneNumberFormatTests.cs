using AmigoAlert.Shared;

namespace AmigoAlert.Tests.Unit
{
	/// <summary>DR-02: either way an Ecuadorian mobile number is typed, it is stored in E.164.</summary>
	[Trait("Level", "Unit")]
	public sealed class PhoneNumberFormatTests
	{
		[Theory]
		[InlineData("0991234567", "+593991234567")]
		[InlineData("099 123 4567", "+593991234567")]
		[InlineData("+593991234567", "+593991234567")]
		[InlineData("+593 99 123 4567", "+593991234567")]
		public void TryNormalize_AcceptsBothFormats(string input, string expected)
		{
			Assert.True(PhoneNumberFormat.TryNormalize(input, out string e164));
			Assert.Equal(expected, e164);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("022123456")]
		[InlineData("099123456")]
		[InlineData("09912345678")]
		[InlineData("+593221234567")]
		[InlineData("+15005550006")]
		public void TryNormalize_RejectsAnythingElse(string? input)
		{
			Assert.False(PhoneNumberFormat.TryNormalize(input, out _));
		}
	}
}
