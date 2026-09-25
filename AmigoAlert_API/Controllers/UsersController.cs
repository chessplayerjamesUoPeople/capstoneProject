using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public sealed class UsersController(IUserRepository userRepository) : ControllerBase
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

			return Ok(UserMappingExtensions.ToDto(user));
		}
	}
}
