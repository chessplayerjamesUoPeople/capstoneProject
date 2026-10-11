using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AmigoAlert.API.Controllers
{
	[Route("api/users")]
	[ApiController]
	public sealed class CheckInsController(ICheckInRepository checkInRepository, IUserRepository userRepository,
		EscalationService escalationService, ILogger<CheckInsController> logger) : ControllerBase
	{
		[HttpGet("{id:guid}/checkins")]
		[ProducesResponseType<IReadOnlyList<CheckInDto>>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<IReadOnlyList<CheckInDto>>> GetAsync(Guid id, CancellationToken ct, DateTimeOffset? since = null)
		{
			if (!await userRepository.ExistsAsync(id, ct))
			{
				return NotFound();
			}

			DateTimeOffset from = since ?? DateTimeOffset.UtcNow.AddDays(-7);
			IReadOnlyList<CheckIn> checkIns = await checkInRepository.GetForUserSinceAsync(id, from, ct);

			return Ok(checkIns.Select(checkIn => checkIn.ToDto()));
		}

		/// <summary>
		/// Records the user's answer to a check-in notification and returns their status to okay. Not user-owned in the route, since the
		/// notification carries only the check-in identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the check-in being answered.</param>
		/// <param name="request">The user's response; only okay is accepted.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once recorded.</returns>
		[HttpPut("/api/checkins/{id:long}/response")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> PutResponseAsync(long id, RespondToCheckInDto request, CancellationToken ct)
		{
			CheckInResponseOutcome outcome = await escalationService.RecordCheckInResponseAsync(id, request.Response, ct);

			return outcome switch
			{
				CheckInResponseOutcome.Recorded => NoContent(),
				CheckInResponseOutcome.NotFound => NotFound(),
				CheckInResponseOutcome.AlreadyAnswered => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "This check-in has already been answered."),
				CheckInResponseOutcome.Superseded => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "A newer check-in has been sent; answer that one instead."),
				CheckInResponseOutcome.UnsupportedResponse => Problem(statusCode: StatusCodes.Status400BadRequest,
					detail: "Only an okay response can be recorded here. Use POST /api/users/{id}/emergency to raise an emergency."),
				_ => throw new UnreachableException()
			};
		}

		/// <summary>
		/// Records the device location when a check-in notification arrives, so that if the user never answers, the alert to the emergency
		/// contact can say where they last were. Discarded when the user answers. Not user-owned in the route, like the response, since the
		/// notification carries only the check-in identifier.
		/// </summary>
		/// <param name="id">The unique identifier of the check-in the device was notified of.</param>
		/// <param name="request">The device location. Both values are required.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once recorded.</returns>
		[HttpPut("/api/checkins/{id:long}/location")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> PutLocationAsync(long id, ActivateEmergencyDto request, CancellationToken ct)
		{
			if (request.Latitude is not double latitude || request.Longitude is not double longitude)
			{
				return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Both latitude and longitude are required.");
			}

			CheckInResponseOutcome outcome = await escalationService.RecordCheckInLocationAsync(id, latitude, longitude, ct);

			return outcome switch
			{
				CheckInResponseOutcome.Recorded => NoContent(),
				CheckInResponseOutcome.NotFound => NotFound(),
				CheckInResponseOutcome.AlreadyAnswered or CheckInResponseOutcome.Superseded => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "This check-in is no longer waiting for an answer."),
				_ => throw new UnreachableException()
			};
		}

		/// <summary>
		/// Records when a check-in notification arrived on the device, so the system can tell whether its notifications reach people and how
		/// long they take. Accepted whether or not the check-in has been answered. Not user-owned in the route, like the response.
		/// </summary>
		/// <remarks>
		/// The first report wins; a repeat, for example from a retried request, changes nothing and still returns 204. The device's time is
		/// logged against the server's, so a skewed device clock is visible in the API log.
		/// </remarks>
		/// <param name="id">The unique identifier of the check-in whose notification arrived.</param>
		/// <param name="request">When the notification arrived, by the device's clock.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once recorded.</returns>
		[HttpPut("/api/checkins/{id:long}/delivery")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PutDeliveryAsync(long id, ReportDeliveryDto request, CancellationToken ct)
		{
			DateTimeOffset serverNow = DateTimeOffset.UtcNow;

			if (await checkInRepository.RecordDeliveryAsync(id, request.DeliveredAt, ct) == 0
				&& await checkInRepository.GetByIdAsync(id, ct) is null)
			{
				return NotFound();
			}

			// Positive is the request's transit time if the clocks agree; negative means the device clock is ahead of the server's.
			logger.LogInformation("Check-in {CheckInId} delivered at {DeliveredAt:O} by the device clock; reported at {ServerNow:O} by the " +
				"server clock ({DifferenceMs:F0} ms later).", id, request.DeliveredAt.ToUniversalTime(), serverNow,
				(serverNow - request.DeliveredAt).TotalMilliseconds);

			return NoContent();
		}
	}
}
