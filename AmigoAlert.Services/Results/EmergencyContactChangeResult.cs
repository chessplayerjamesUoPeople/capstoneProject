using AmigoAlert.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Services.Results
{
	/// <summary>
	/// The outcome of adding or replacing an emergency contact, with the resulting active contact when the change succeeded.
	/// </summary>
	/// <param name="Outcome">What happened.</param>
	/// <param name="Contact">The active contact after the change, or null when the change did not succeed.</param>
	public sealed record EmergencyContactChangeResult(EmergencyContactChangeOutcome Outcome, EmergencyContact? Contact);
}
