namespace MobileApplication.Services
{
	/// <summary>
	/// Sends a GET once more when it fails to reach the API. After the phone sleeps, changes network or idles, the pooled connection
	/// has often been closed underneath the client, and the first request on it fails at once although the second one succeeds.
	/// Only GETs are retried: a repeated POST such as the emergency button could alert the contact twice.
	/// </summary>
	public sealed class TransientRetryHandler : DelegatingHandler
	{
		private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.Method != HttpMethod.Get)
			{
				return await base.SendAsync(request, cancellationToken);
			}

			try
			{
				return await base.SendAsync(request, cancellationToken);
			}
			catch (HttpRequestException)
			{
				await Task.Delay(RetryDelay, cancellationToken);

				return await base.SendAsync(request, cancellationToken);
			}
		}
	}
}
