using System.Net;

namespace MobileApplication.Services
{
	/// <summary>
	/// The API answered with a non-success status. <see cref="Exception.Message"/> is the API's ProblemDetails detail when it sent one,
	/// so it can be shown to the user as is.
	/// </summary>
	public sealed class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
	{
		/// <summary>The HTTP status the API returned.</summary>
		public HttpStatusCode StatusCode { get; } = statusCode;
	}
}
