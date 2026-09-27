using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// Consent flow settings, bound from the <c>Consent</c> configuration section.
	/// </summary>
	public sealed class ConsentOptions
	{
		/// <summary>The configuration section these options bind from.</summary>
		public const string SectionName = "Consent";

		/// <summary>
		/// The base URL of the Blazor consent page, including the trailing slash (e.g. https://amigoalert.example/consent/). The token is
		/// appended in its 32-character "N" form to keep the consent SMS within the length limit.
		/// </summary>
		public string PageBaseUrl { get; set; } = string.Empty;
	}
}
