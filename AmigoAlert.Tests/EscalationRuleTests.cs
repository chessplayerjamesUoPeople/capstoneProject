using AmigoAlert.Services.Rules;
using AmigoAlert.Shared;
using AmigoAlert.Shared.Enums;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Tests
{
	public class EscalationRuleTests
	{
		private static readonly DateTimeOffset Now =
			new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(-5));

		private static readonly DateOnly Today = new(2026, 10, 4);

		private static readonly TimeOnly MorningCheckIn = new(8, 0);

		private static readonly TimeOnly AfternoonCheckIn = new(18, 0);

		[Fact]
		public void Evaluate_WhenNoScheduleConfigured_ReturnsNone()
		{
			//Arrange
			EscalationRules.Input input = new(null, null, StatusType.Okay, null, null, null);

			//Act
			EscalationOutcome result = EscalationRules.Evaluate(input, Now).Outcome;

			//Assert
			Assert.Equal(EscalationOutcome.None, result);
		}

		[Fact]
		public void Evaluate_JustSignedUp_ReturnsSendFirstNotification()
		{
			//Arrange
			EscalationRules.Input input = new(MorningCheckIn, AfternoonCheckIn, StatusType.Okay, null, null, null);

			//Act
			EscalationOutcome result = EscalationRules.Evaluate(input, Now).Outcome;

			//Assert
			Assert.Equal(EscalationOutcome.SendFirstNotification, result);
		}

		[Fact]
		public void Evaluate_ElevenMinutesAfterFirstCheckIn_ReturnsSendReminderNotification()
		{
			//Arrange
			EscalationRules.Input input = new(MorningCheckIn, AfternoonCheckIn, StatusType.Pending1,
				new(Today.ToDateTime(MorningCheckIn), EscalationRules.LocalOffset), Now.AddSeconds(10), null);

			//Act
			EscalationOutcome result = EscalationRules.Evaluate(input, Now.AddMinutes(11)).Outcome;

			//Assert
			Assert.Equal(EscalationOutcome.SendReminderNotification, result);
		}

		[Fact]
		public void Evaluate_ElevenMinutesAfterReminder_ReturnsEscalateToEmergencyContact()
		{
			//Arrange
			EscalationRules.Input input = new(MorningCheckIn, AfternoonCheckIn, StatusType.Pending2,
				new(Today.ToDateTime(MorningCheckIn), EscalationRules.LocalOffset), Now.AddMinutes(10).AddSeconds(10), null);

			//Act
			EscalationOutcome result = EscalationRules.Evaluate(input, Now.AddMinutes(21)).Outcome;

			//Assert
			Assert.Equal(EscalationOutcome.EscalateToEmergencyContact, result);
		}

		[Fact]
		public void MostRecentOccurrence_BothTimesNull_ReturnsNull()
		{
			//Arrange

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(null, null, Now);

			//Assert
			Assert.Null(result);
		}

		[Fact]
		public void MostRecentOccurrence_NowExactlyOccurrence_ReturnsOccurrence()
		{
			//Arrange
			DateTimeOffset now = Now.AddHours(-4);

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(MorningCheckIn, AfternoonCheckIn, now);

			//Assert
			Assert.Equal(now, result);
		}

		[Fact]
		public void MostRecentOccurrence_OneSecondBeforeMorningCheckIn_ReturnsPreviousDayOccurrence()
		{
			//Arrange
			DateTimeOffset expected = new(Today.AddDays(-1).ToDateTime(AfternoonCheckIn), EscalationRules.LocalOffset);
			DateTimeOffset now = Now.AddHours(-4).AddSeconds(-1);

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(MorningCheckIn, AfternoonCheckIn, now);

			//Assert
			Assert.Equal(expected, result);
		}

		[Fact]
		public void MostRecentOccurrence_AfterBothCheckIns_ReturnsAfternoonOccurrence()
		{
			//Arrange
			DateTimeOffset now = Now.AddHours(10);

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(MorningCheckIn, AfternoonCheckIn, now);

			//Assert
			Assert.Equal(new(Today.ToDateTime(AfternoonCheckIn), EscalationRules.LocalOffset), result);
		}

		[Fact]
		public void MostRecentOccurrence_Midnight_ReturnsPreviousDayOccurrence()
		{
			//Arrange
			DateTimeOffset expected = new(Today.AddDays(-1).ToDateTime(AfternoonCheckIn), EscalationRules.LocalOffset);
			DateTimeOffset now = Now.AddHours(-12);

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(MorningCheckIn, AfternoonCheckIn, now);

			//Assert
			Assert.Equal(expected, result);
		}

		[Fact]
		public void MostRecentOccurrence_MidnightUTC_ReturnsPreviousDayOccurrence()
		{
			//Arrange
			DateTimeOffset expected = new(Today.AddDays(-1).ToDateTime(AfternoonCheckIn), EscalationRules.LocalOffset);
			DateTimeOffset now = new(2026, 10, 04, 00, 00, 00, TimeSpan.Zero);

			//Act
			DateTimeOffset? result = EscalationRules.MostRecentOccurrence(MorningCheckIn, AfternoonCheckIn, now);

			//Assert
			Assert.Equal(expected, result);
		}
	}
}
