using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services
{
	/// <summary>
	/// One email, as composed by <see cref="EmailMessageComposer"/>.
	/// </summary>
	/// <param name="Subject">The subject line.</param>
	/// <param name="PlainText">The plain-text body.</param>
	/// <param name="Html">The HTML body, with the same content as <paramref name="PlainText"/>.</param>
	public sealed record EmailContent(string Subject, string PlainText, string Html);
}
