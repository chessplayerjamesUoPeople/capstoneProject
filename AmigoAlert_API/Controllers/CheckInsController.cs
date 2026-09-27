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
		EscalationService escalationService) : ControllerBase
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
	}
}
