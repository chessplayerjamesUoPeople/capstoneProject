using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/users")]
	public sealed class EmergencyContactsController(IUserRepository userRepository) : ControllerBase
	{
		/// <summary>
		/// Returns the user's active emergency contact and its consent state.
		/// </summary>
		[HttpGet("{id:guid}/emergency-contact")]
		[ProducesResponseType<EmergencyContactDto>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<ActionResult<EmergencyContactDto>> GetAsync(Guid id, CancellationToken ct)
		{
			EmergencyContact? contact = await userRepository.GetEmergencyContactAsync(id, ct);

			if (contact is null)
			{
				return NotFound();
			}

			return Ok(contact.ToDto());
		}
	}
}
