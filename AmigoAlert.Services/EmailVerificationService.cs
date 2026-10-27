using AmigoAlert.Data;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Issues and checks the one-time codes emailed to prove that the person using the app can read an inbox. A code confirms the address
	/// at registration and stands in for a password at log in.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Only the latest code for an address and purpose works. A code is guessable in at most <see cref="MaxFailedAttempts"/> tries, after
	/// which a new one must be requested, and requests are limited by <see cref="ResendCooldown"/> and <see cref="MaxCodesPerHour"/>.
	/// </para>
	/// <para>
	/// Checking and using a code are separate steps, <see cref="MatchCodeAsync"/> and <see cref="ConsumeAsync"/>. A wrong guess is recorded
	/// at once, outside any transaction, so it counts even though the caller then writes nothing; the code is used up inside the caller's
	/// transaction, so it stays usable if the caller's own write fails.
	/// </para>
	/// </remarks>
	public sealed class EmailVerificationService(IEmailVerificationRepository verificationRepository, IUserRepository userRepository,
		IEmailService emailService, ITransactionProvider transactionProvider, TimeProvider timeProvider)
	{
		/// <summary>The number of digits in a code.</summary>
		public const int CodeLength = VerificationCodeFormat.Length;

		/// <summary>The number of wrong codes after which a code stops working, even if the right one is then entered.</summary>
		public const int MaxFailedAttempts = 5;

		/// <summary>The number of codes that can be sent to one address for one purpose in any hour.</summary>
		public const int MaxCodesPerHour = 5;

		/// <summary>How long a code remains usable. Long enough to switch to the mail app and back, even when the email is slow.</summary>
		public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

		/// <summary>The shortest gap between two codes sent to one address for one purpose.</summary>
		public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

		private static readonly TimeSpan RateWindow = TimeSpan.FromHours(1);

		private const int SaltLength = 16;

		/// <summary>
		/// Emails a new code to <paramref name="email"/>, superseding any earlier one for <paramref name="purpose"/>. A registration code is
		/// sent only to an unregistered address and a log in code only to a registered one.
		/// </summary>
		/// <param name="email">The address to send the code to.</param>
		/// <param name="purpose">What the code will be used for.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>Whether the code was sent, and if not, why.</returns>
		public async Task<VerificationCodeRequestResult> RequestCodeAsync(string email, VerificationPurposeType purpose,
			CancellationToken ct = default)
		{
			string address = Normalize(email);
			bool registered = await userRepository.EmailExistsAsync(address, ct);

			if (purpose == VerificationPurposeType.Registration && registered)
			{
				return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.EmailAlreadyRegistered);
			}

			if (purpose == VerificationPurposeType.Login && !registered)
			{
				return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.EmailNotRegistered);
			}

			DateTimeOffset now = timeProvider.GetUtcNow();
			IReadOnlyList<DateTimeOffset> issued = await verificationRepository.GetIssueTimesSinceAsync(address, purpose, now - RateWindow,
				ct);

			if (issued.Count > 0 && issued[^1] + ResendCooldown > now)
			{
				return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.TooManyRequests, issued[^1] + ResendCooldown - now);
			}

			if (issued.Count >= MaxCodesPerHour)
			{
				// The next request succeeds once the oldest of the last MaxCodesPerHour codes falls out of the window.
				return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.TooManyRequests,
					issued[issued.Count - MaxCodesPerHour] + RateWindow - now);
			}

			string code = RandomNumberGenerator.GetString("0123456789", CodeLength);
			byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);

			EmailVerification verification = new EmailVerification
			{
				Email = address,
				PurposeId = (byte)purpose,
				CodeHash = Hash(salt, code),
				CodeSalt = salt,
				CreatedAt = now,
				ExpiresAt = now + CodeLifetime
			};

			// The email is sent inside the transaction, which commits only if the provider accepted it: a failed send leaves the previous
			// code working and does not count towards the limits.
			await using IDbContextTransaction transaction = await transactionProvider.BeginTransactionAsync(ct);

			await verificationRepository.ExpireActiveAsync(address, purpose, now, ct);
			await verificationRepository.AddAsync(verification, ct);

			EmailDispatchResult dispatch = await emailService.SendAsync(email.Trim(),
				EmailMessageComposer.VerificationCode(code, purpose, CodeLifetime), ct);

			if (!dispatch.Succeeded)
			{
				return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.SendFailed);
			}

			await transaction.CommitAsync(ct);

			return new VerificationCodeRequestResult(VerificationCodeRequestOutcome.Sent);
		}

		/// <summary>
		/// Checks <paramref name="code"/> against the latest code sent to <paramref name="email"/> for <paramref name="purpose"/>, without
		/// using it up. A wrong code is recorded against the verification immediately.
		/// </summary>
		/// <param name="email">The address the code was sent to.</param>
		/// <param name="purpose">What the code is being used for.</param>
		/// <param name="code">The code the user entered.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The verification to pass to <see cref="ConsumeAsync"/>, or null when the code does not work.</returns>
		public async Task<long?> MatchCodeAsync(string email, VerificationPurposeType purpose, string code, CancellationToken ct = default)
		{
			EmailVerification? verification = await verificationRepository.GetLatestActiveAsync(Normalize(email), purpose,
				timeProvider.GetUtcNow(), ct);

			if (verification is null || verification.FailedAttempts >= MaxFailedAttempts)
			{
				return null;
			}

			if (!CryptographicOperations.FixedTimeEquals(Hash(verification.CodeSalt, code.Trim()), verification.CodeHash))
			{
				await verificationRepository.IncrementFailedAttemptsAsync(verification.Id, ct);
				return null;
			}

			return verification.Id;
		}

		/// <summary>
		/// Uses up a code that <see cref="MatchCodeAsync"/> accepted. Call it inside the transaction that acts on the code.
		/// </summary>
		/// <param name="verificationId">The verification returned by <see cref="MatchCodeAsync"/>.</param>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>True when the code was used up by this call; false when it was used, superseded or expired in the meantime.</returns>
		public async Task<bool> ConsumeAsync(long verificationId, CancellationToken ct = default)
		{
			return await verificationRepository.ConsumeAsync(verificationId, timeProvider.GetUtcNow(), ct) == 1;
		}

		/// <summary>
		/// The form addresses are stored and matched in. Users' own addresses are compared by the database collation, which already
		/// ignores case.
		/// </summary>
		private static string Normalize(string email) => email.Trim().ToLowerInvariant();

		private static byte[] Hash(byte[] salt, string code)
		{
			return SHA256.HashData([.. salt, .. Encoding.UTF8.GetBytes(code)]);
		}
	}
}
