using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Composes the emails sent to users. Pure and deterministic, like <see cref="SmsMessageComposer"/>.
	/// </summary>
	public static class EmailMessageComposer
	{
		/// <summary>
		/// The email carrying a verification code. The code is in the subject too, so it can be read from the notification without opening
		/// the message.
		/// </summary>
		/// <param name="code">The verification code.</param>
		/// <param name="purpose">What the code is for, which decides the opening line.</param>
		/// <param name="lifetime">How long the code remains usable.</param>
		/// <returns>The subject and bodies.</returns>
		public static EmailContent VerificationCode(string code, VerificationPurposeType purpose, TimeSpan lifetime)
		{
			string intro = purpose switch
			{
				VerificationPurposeType.Registration => "Use this code to confirm your email address and finish registering with AmigoAlert:",
				VerificationPurposeType.Login => "Use this code to log in to AmigoAlert:",
				_ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
			};
			string expiry = string.Create(CultureInfo.InvariantCulture, $"The code expires in {(int)lifetime.TotalMinutes} minutes.");
			const string ignore = "If you did not ask for this code, you can ignore this email. Nobody can use your email address without it.";

			string plainText = $"{intro}\r\n\r\n{code}\r\n\r\n{expiry}\r\n\r\n{ignore}\r\n";
			string html = $"<p>{intro}</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:6px\">{code}</p><p>{expiry}</p>" +
				$"<p>{ignore}</p>";

			return new EmailContent($"Your AmigoAlert code is {code}", plainText, html);
		}
	}
}
