using AmigoAlert.Data.Repositories;
using AmigoAlert.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmigoAlert.API.Controllers
{
	[ApiController]
	[Route("api/sms")]
	public sealed class SmsController(ISmsService smsService, ISmsRepository smsRepository) : ControllerBase
	{
		/// <summary>
		/// Twilio's delivery status callback, which feeds the SMS delivery success metric. Unauthenticated, so every request must carry a
		/// valid <c>X-Twilio-Signature</c>; without that check anyone could post fake delivery confirmations and corrupt the metric.
		/// </summary>
		/// <param name="messageSid">The Twilio SID of the message the status refers to.</param>
		/// <param name="messageStatus">The reported status, for example "delivered" or "undelivered".</param>
		/// <param name="errorCode">The Twilio error code when delivery failed, otherwise absent.</param>
		/// <param name="ct">The cancellation token.</param>
		/// <returns>No content once processed, including for unknown or already-final messages.</returns>
		[HttpPost("status")]
		[Consumes("application/x-www-form-urlencoded")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> PostStatusAsync([FromForm(Name = "MessageSid")] string messageSid,
			[FromForm(Name = "MessageStatus")] string messageStatus, [FromForm(Name = "ErrorCode")] int? errorCode, CancellationToken ct)
		{
			// Twilio signs every posted field, not only the ones bound above.
			Dictionary<string, string> parameters = Request.Form.ToDictionary(field => field.Key, field => field.Value.ToString());

			if (!smsService.IsValidStatusCallback(parameters, Request.Headers["X-Twilio-Signature"]))
			{
				return StatusCode(StatusCodes.Status403Forbidden);
			}

			await smsRepository.RecordDeliveryStatusAsync(messageSid, messageStatus, errorCode, ct);

			return NoContent();
		}
	}
}
