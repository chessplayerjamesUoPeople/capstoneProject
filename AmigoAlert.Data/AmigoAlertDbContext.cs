using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data
{
	/// <summary>
	/// Represents the database context for the AmigoAlert application,
	/// providing access to the underlying database and managing entity configurations.
	/// </summary>
	public class AmigoAlertDbContext : DbContext
	{
		public AmigoAlertDbContext(DbContextOptions<AmigoAlertDbContext> options) : base(options)
		{
		}

		public DbSet<CheckIn> CheckIns => Set<CheckIn>();
		public DbSet<CheckInResponse> CheckInResponses => Set<CheckInResponse>();
		public DbSet<ConsentStatus> ConsentStatuses => Set<ConsentStatus>();
		public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
		public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
		public DbSet<Sms> Sms => Set<Sms>();
		public DbSet<SmsMessageType> SmsMessageTypes => Set<SmsMessageType>();
		public DbSet<Status> Statuses => Set<Status>();
		public DbSet<StatusLog> StatusLogs => Set<StatusLog>();
		public DbSet<User> Users => Set<User>();
		public DbSet<VerificationPurpose> VerificationPurposes => Set<VerificationPurpose>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyConfigurationsFromAssembly(typeof(AmigoAlertDbContext).Assembly);
		}
	}
}
