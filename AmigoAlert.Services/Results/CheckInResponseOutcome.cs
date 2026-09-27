using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The result of recording a user's answer to a check-in.
	/// </summary>
	public enum CheckInResponseOutcome : byte
	{
		/// <summary>The answer was recorded and the user's status returned to okay.</summary>
		Recorded = 1,

		/// <summary>No check-in has the given identifier.</summary>
		NotFound = 2,

		/// <summary>The occurrence had already been answered, possibly by a concurrent request.</summary>
		AlreadyAnswered = 3,

		/// <summary>The check-in belongs to an older occurrence than the latest; answering it would reset the current one.</summary>
		Superseded = 4,

		/// <summary>The response was not <c>Okay</c>. Emergencies go through the emergency endpoint.</summary>
		UnsupportedResponse = 5
	}
}
