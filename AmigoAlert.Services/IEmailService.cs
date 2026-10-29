using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Sends email to users. Unlike <see cref="ISmsService"/>, nothing is recorded in the database: the only email sent is a verification
	/// code, and its <c>EmailVerification</c> row is the record.
	/// </summary>
	public interface IEmailService
	{
		/// <summary>
		/// Hands one email to the provider. Never throws for a provider failure; the failure is logged and reported in the result.
		/// </summary>
		/// <param name="toEmail">The recipient's address.</param>
		/// <param name="content">The subject and bodies.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the provider accepted the message.</returns>
		Task<EmailDispatchResult> SendAsync(string toEmail, EmailContent content, CancellationToken ct = default);
	}
}
