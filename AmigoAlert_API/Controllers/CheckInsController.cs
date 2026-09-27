using AmigoAlert.API.Mapping;
using AmigoAlert.Data.Entities;
using AmigoAlert.Data.Repositories;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[Route("api/users")]
	[ApiController]
	public sealed class CheckInsController(ICheckInRepository checkInRepository, IUserRepository userRepository) : ControllerBase
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
	}
}
