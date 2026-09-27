using System;
using System.Collections.Generic;
using System.Text;

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
	}
}
