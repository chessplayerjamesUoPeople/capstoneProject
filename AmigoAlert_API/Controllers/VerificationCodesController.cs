using AmigoAlert.Services;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/verification-codes")]
	public sealed class VerificationCodesController(EmailVerificationService verificationService) : ControllerBase
	{
		/// <summary>
		/// Emails a one-time code for registering or logging in. Any earlier code for the same address and purpose stops working.
		/// </summary>
		/// <remarks>
		/// The 404 and 409 responses tell the caller whether an address is registered. Registration already did so, and the app needs it to
		/// send the user to the right screen; it is recorded with the API's other unauthenticated-access risks.
		/// </remarks>
		/// <param name="request">The address and what the code is for.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>Accepted once the email has been handed to the provider.</returns>
		[HttpPost]
		[ProducesResponseType(StatusCodes.Status202Accepted)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
		public async Task<IActionResult> PostAsync(RequestVerificationCodeDto request, CancellationToken ct)
		{
			VerificationCodeRequestResult result = await verificationService.RequestCodeAsync(request.Email, request.Purpose, ct);

			if (result.RetryAfter is TimeSpan retryAfter)
			{
				Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
			}

			return result.Outcome switch
			{
				VerificationCodeRequestOutcome.Sent => Accepted(),
				VerificationCodeRequestOutcome.EmailAlreadyRegistered => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "The email address is already registered. Log in instead."),
				VerificationCodeRequestOutcome.EmailNotRegistered => Problem(statusCode: StatusCodes.Status404NotFound,
					detail: "No account uses this email address. Register instead."),
				VerificationCodeRequestOutcome.TooManyRequests => Problem(statusCode: StatusCodes.Status429TooManyRequests,
					detail: "Too many codes were requested for this email address. Wait a little, then try again."),
				VerificationCodeRequestOutcome.SendFailed => Problem(statusCode: StatusCodes.Status502BadGateway,
					detail: "The email could not be sent. Nothing was recorded; retry the request."),
				_ => throw new UnreachableException()
			};
		}
	}
}
