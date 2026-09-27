using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services;
using AmigoAlert.Services.Results;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/users")]
	public sealed class EmergencyContactsController(IEmergencyContactRepository contactRepository,
		ConsentService consentService) : ControllerBase
	{
		/// <summary>
		/// Returns the user's active emergency contact and its consent state.
		/// </summary>
		[HttpGet("{id:guid}/emergency-contact")]
		[ProducesResponseType<EmergencyContactDto>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<EmergencyContactDto>> GetAsync(Guid id, CancellationToken ct)
		{
			EmergencyContact? contact = await contactRepository.GetByUserIdAsync(id, ct);

			if (contact is null)
			{
				return NotFound();
			}

			return Ok(contact.ToDto());
		}

		/// <summary>
		/// Adds the user's first emergency contact at pending consent and sends it a consent request SMS.
		/// </summary>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="request">The contact's name and phone number.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>The new contact, with its location in the Location header.</returns>
		[HttpPost("{id:guid}/emergency-contact")]
		[ProducesResponseType<EmergencyContactDto>(StatusCodes.Status201Created)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
		public async Task<ActionResult<EmergencyContactDto>> PostAsync(Guid id, SetEmergencyContactDto request, CancellationToken ct)
		{
			EmergencyContactChangeResult result = await consentService.AddContactAsync(id, request.Name, request.Phone, ct);

			if (result.Contact is not null)
			{
				return CreatedAtAction(nameof(GetAsync), new { id }, result.Contact.ToDto());
			}

			return Failure(result.Outcome);
		}

		/// <summary>
		/// Replaces the user's emergency contact. A new phone number deactivates the old contact and sends a new consent request; a
		/// name-only change keeps the existing consent.
		/// </summary>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="request">The contact's name and phone number.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content on success.</returns>
		[HttpPut("{id:guid}/emergency-contact")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
		public async Task<IActionResult> PutAsync(Guid id, SetEmergencyContactDto request, CancellationToken ct)
		{
			EmergencyContactChangeResult result = await consentService.ReplaceContactAsync(id, request.Name, request.Phone, ct);

			if (result.Contact is not null)
			{
				return NoContent();
			}

			return Failure(result.Outcome);
		}

		private ActionResult Failure(EmergencyContactChangeOutcome outcome)
		{
			return outcome switch
			{
				EmergencyContactChangeOutcome.ContactAlreadyExists => Problem(statusCode: StatusCodes.Status409Conflict,
					detail: "The user already has an emergency contact; use PUT to replace it."),
				EmergencyContactChangeOutcome.SmsFailed => Problem(statusCode: StatusCodes.Status502BadGateway,
					detail: "The SMS provider rejected the consent request. Nothing was changed; retry the request."),
				_ => NotFound()
			};
		}
	}
}
