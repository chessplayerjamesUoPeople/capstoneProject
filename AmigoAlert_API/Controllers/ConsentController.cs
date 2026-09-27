using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/consent")]
	public sealed class ConsentController(IUserRepository userRepository) : ControllerBase
	{
		/// <summary>
		/// Returns the consent request identified by the token. Unauthenticated:
		/// the emergency contact has no account, so token entropy is the only control.
		/// </summary>
		[HttpGet("{token:guid}")]
		[ProducesResponseType<ConsentRequestDto>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<ConsentRequestDto>> GetAsync(Guid token, CancellationToken ct)
		{
			EmergencyContact? contact = await userRepository.GetEmergencyContactByConsentTokenAsync(token, ct);

			if (contact is null)
			{
				return NotFound();
			}

			return Ok(contact.ToConsentRequestDto(DateTimeOffset.UtcNow));
		}
	}
}
