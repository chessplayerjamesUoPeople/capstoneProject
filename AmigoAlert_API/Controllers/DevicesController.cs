using AmigoAlert.Data.Repositories;
using AmigoAlert.Services;
using AmigoAlert.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/users")]
	public sealed class DevicesController(IUserRepository userRepository, IPushNotificationService pushService) : ControllerBase
	{
		/// <summary>
		/// Registers one of the user's devices for check-in push notifications, or moves an existing registration to a new push channel.
		/// The app calls it on every launch and whenever FCM issues a new token.
		/// </summary>
		/// <remarks>
		/// Like the rest of the API this is not yet authenticated, so anyone who knows a user's identifier could register a device to receive
		/// that user's check-ins. The server, not the caller, decides which notifications a device receives.
		/// </remarks>
		/// <param name="id">The unique identifier of the user.</param>
		/// <param name="installationId">The identifier the app generated for this installation; stable across launches.</param>
		/// <param name="request">The device's FCM registration token.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once the notification service has accepted the registration.</returns>
		[HttpPut("{id:guid}/devices/{installationId:guid}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
		public async Task<IActionResult> PutAsync(Guid id, Guid installationId, RegisterDeviceDto request, CancellationToken ct)
		{
			if (!await userRepository.ExistsAsync(id, ct))
			{
				return NotFound();
			}

			if (!await pushService.RegisterDeviceAsync(id, installationId, request.PushChannel, ct))
			{
				return Problem(statusCode: StatusCodes.Status502BadGateway,
					detail: "The notification service rejected the registration; retry the request.");
			}

			return NoContent();
		}
	}
}
