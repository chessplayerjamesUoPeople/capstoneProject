using AmigoAlert.Shared;
using AmigoAlert.Shared.DTOs;
using AmigoAlert.Shared.Enums;

namespace MobileApplication.Services
{
	/// <summary>Which variant of the home screen to show.</summary>
	public enum HomeState
	{
		/// <summary>The user has no active emergency contact; they must add one (US-08).</summary>
		NoContact,

		/// <summary>The contact has not answered the consent request yet; the app is locked (US-07).</summary>
		AwaitingConsent,

		/// <summary>The contact declined; the user must enter a new one (US-08).</summary>
		ConsentRejected,

		/// <summary>Nothing outstanding; show the time until the next check-in (US-11).</summary>
		Okay,

		/// <summary>A check-in notification is waiting for an answer (US-12).</summary>
		CheckInPending,

		/// <summary>A missed check-in was escalated to the contact (US-13).</summary>
		Escalated,

		/// <summary>The user pressed the emergency button (US-14).</summary>
		Emergency
	}

	/// <summary>
	/// The home screen's state, worked out from the user and their emergency contact.
	/// </summary>
	/// <param name="State">Which variant of the home screen to show.</param>
	/// <param name="ContactName">The emergency contact's name, or null when there is none.</param>
	/// <param name="NextCheckIn">The next scheduled check-in, or null when the user has no check-in times.</param>
	public sealed record HomeStatus(HomeState State, string? ContactName, DateTimeOffset? NextCheckIn)
	{
		public static HomeStatus Create(UserDto user, EmergencyContactDto? contact, DateTimeOffset now)
		{
			DateTimeOffset? next = CheckInSchedule.NextOccurrence(user.Notification1Time, user.Notification2Time, now);

			// Consent comes first: the server only runs check-ins for users whose contact has granted it.
			HomeState state = contact?.ConsentStatus switch
			{
				null => HomeState.NoContact,
				ConsentStatusType.Rejected or ConsentStatusType.Revoked => HomeState.ConsentRejected,
				ConsentStatusType.Pending or ConsentStatusType.Expired => HomeState.AwaitingConsent,
				_ => user.Status switch
				{
					StatusType.Pending1 or StatusType.Pending2 => HomeState.CheckInPending,
					StatusType.Escalated => HomeState.Escalated,
					StatusType.Emergency => HomeState.Emergency,
					_ => HomeState.Okay
				}
			};

			return new HomeStatus(state, contact?.Name, next);
		}
	}
}
