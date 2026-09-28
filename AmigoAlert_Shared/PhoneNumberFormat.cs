using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AmigoAlert.Shared
{
	/// <summary>
	/// Validation patterns for phone numbers, shared so the mobile client and the API reject the same input.
	/// </summary>
	public static class PhoneNumberFormat
	{
		/// <summary>
		/// E.164: a leading plus, a non-zero country code and at most 15 digits in total (e.g. +593991234567).
		/// </summary>
		public const string E164 = @"^\+[1-9]\d{7,14}$";

		/// <summary>
		/// Accepts an Ecuadorian mobile number as 09XXXXXXXX or +5939XXXXXXXX (spaces ignored)
		/// and returns it in E.164 (+5939XXXXXXXX), the format it is stored in.
		/// </summary>
		public static bool TryNormalize(string? input, out string e164)
		{
			var digits = (input ?? "").Replace(" ", "");

			if (Regex.IsMatch(digits, @"^09\d{8}$"))
				digits = "+593" + digits[1..];

			e164 = digits;
			return Regex.IsMatch(digits, @"^\+5939\d{8}$");
		}
	}
}
