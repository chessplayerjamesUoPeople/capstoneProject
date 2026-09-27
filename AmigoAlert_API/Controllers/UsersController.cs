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
	[Route("api/[controller]")]
	[ApiController]
	public sealed class UsersController(IUserRepository userRepository, RegistrationService registrationService,
		EscalationService escalationService) : ControllerBase
	{
		/// <summary>
		/// Returns the user's profile, notification schedule and current status.
		/// </summary>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>The user's profile, notification schedule and current status.</returns>
		[HttpGet("{id:guid}")]
		[ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<UserDto>> GetAsync(Guid id, CancellationToken ct)
		{
			User? user = await userRepository.GetByIdAsync(id, ct);

			if (user is null)
			{
				return NotFound();
			}

			return Ok(user.ToDto());
		}

		/// <summary>
		/// Registers a new user at an initial status of okay, recording acceptance of the submitted terms version.
		/// </summary>
		/// <param name="request">The registration details.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>The registered user, with its location in the Location header.</returns>
		[HttpPost]
		[ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<ActionResult<UserDto>> PostAsync(CreateUserDto request, CancellationToken ct)
		{
			User user = request.ToEntity();

			if (!await registrationService.RegisterAsync(user, ct))
			{
				return Problem(statusCode: StatusCodes.Status409Conflict, detail: "The email address is already registered.");
			}

			return CreatedAtAction(nameof(GetAsync), new { id = user.Id }, user.ToDto());
		}

		/// <summary>
		/// Updates the user's profile.
		/// </summary>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="request">The new profile.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content on success.</returns>
		[HttpPut("{id:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PutAsync(Guid id, UpdateUserDto request, CancellationToken ct)
		{
			if (await userRepository.UpdateProfileAsync(id, request.Name, request.Phone, request.Address, request.Landmarks, ct) == 0)
			{
				return NotFound();
			}

			return NoContent();
		}

		/// <summary>
		/// The manual emergency button: sets the user's status to emergency, records the location if supplied, and alerts the emergency
		/// contact by SMS. Requires an active contact who has granted consent.
		/// </summary>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="request">The device location at the time of the press, if available.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once the alert has been accepted for delivery.</returns>
		[HttpPost("{id:guid}/emergency")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
		public async Task<IActionResult> PostEmergencyAsync(Guid id, ActivateEmergencyDto? request, CancellationToken ct)
		{
			EmergencyActivationOutcome outcome = await escalationService.ActivateEmergencyAsync(id, request?.Latitude, request?.Longitude,
				ct);

			return outcome switch
			{
				EmergencyActivationOutcome.Alerted => NoContent(),
				EmergencyActivationOutcome.UserNotFound => NotFound(),
				EmergencyActivationOutcome.NoConsentedContact => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "The user has no emergency contact who has granted consent, so no alert was sent."),
				EmergencyActivationOutcome.AlertFailed => Problem(statusCode: StatusCodes.Status502BadGateway,
					detail: "The SMS provider rejected the alert. Nothing was recorded; retry the request."),
				_ => throw new UnreachableException()
			};
		}
	}
}
