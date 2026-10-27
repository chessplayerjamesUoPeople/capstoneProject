using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Shared.DTOs
{
	/// <summary>
	/// The shape of an emailed verification code, shared by the API's validation and the app's input field.
	/// </summary>
	public static class VerificationCodeFormat
	{
		/// <summary>The number of digits in a code.</summary>
		public const int Length = 6;

		/// <summary>Exactly <see cref="Length"/> ASCII digits.</summary>
		public const string Pattern = "^[0-9]{6}$";
	}
}
