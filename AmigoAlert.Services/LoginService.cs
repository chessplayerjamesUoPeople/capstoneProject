using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Logs existing users in with a code emailed by <see cref="EmailVerificationService"/>, so the app can recover its user on a new
	/// device or after its data was cleared.
	/// </summary>
	/// <remarks>
	/// Logging in returns the user's identifier, exactly as registration does; the API has no sessions yet, so nothing else is issued.
	/// </remarks>
	public sealed class LoginService(IUserRepository userRepository, EmailVerificationService verificationService)
	{
		/// <summary>
		/// Logs the user registered with <paramref name="email"/> in, using up the code.
		/// </summary>
		/// <param name="email">The address the user registered with.</param>
		/// <param name="verificationCode">The log in code emailed to that address.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The user, or null when the code does not work.</returns>
		public async Task<User?> LogInAsync(string email, string verificationCode, CancellationToken ct = default)
		{
			if (await verificationService.MatchCodeAsync(email, VerificationPurposeType.Login, verificationCode, ct) is not { } verificationId
				|| !await verificationService.ConsumeAsync(verificationId, ct))
			{
				return null;
			}

			return await userRepository.GetByEmailAsync(email.Trim(), ct);
		}
	}
}
