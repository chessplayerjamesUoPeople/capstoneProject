using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/consent")]
	public sealed class ConsentController(IEmergencyContactRepository contactRepository, ConsentService consentService) : ControllerBase
	{
		/// <summary>
		/// Returns the consent request identified by the token. Unauthenticated: the emergency contact has no account, so token entropy is
		/// the only control.
		/// </summary>
		[HttpGet("{token:guid}")]
		[ProducesResponseType<ConsentRequestDto>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<ConsentRequestDto>> GetAsync(Guid token, CancellationToken ct)
		{
			EmergencyContact? contact = await contactRepository.GetByConsentTokenAsync(token, ct);

			if (contact is null)
			{
				return NotFound();
			}

			return Ok(contact.ToConsentRequestDto(DateTimeOffset.UtcNow));
		}

		/// <summary>
		/// Records the contact's answer to the consent request: granting or rejecting a pending request, withdrawing granted consent
		/// (<c>Granted = false</c>), or granting again after a withdrawal. The token stays valid so the contact can change their answer
		/// later. Unauthenticated, like the GET: missing, expired and inapplicable answers all return 404 so the response reveals nothing
		/// about which applies.
		/// </summary>
		/// <param name="token">The consent token from the link.</param>
		/// <param name="request">Whether the contact grants consent, or rejects or withdraws it.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once recorded.</returns>
		[HttpPut("{token:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PutAsync(Guid token, RespondToConsentDto request, CancellationToken ct)
		{
			if (!await consentService.RespondAsync(token, request.Granted, ct))
			{
				return NotFound();
			}

			return NoContent();
		}
	}
}
