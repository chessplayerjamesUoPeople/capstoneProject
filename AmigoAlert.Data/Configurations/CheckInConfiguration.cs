using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the CheckIn entity in the database.
	/// </summary>
	internal class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
	{
		public void Configure(EntityTypeBuilder<CheckIn> builder)
		{
			builder.ToTable("CheckIn");
			builder.HasKey(c => c.Id);
			builder.Property(c => c.UserId).ValueGeneratedOnAdd();
			builder.Property(c => c.ScheduledFor).IsRequired();
			builder.Property(c => c.TimeSent).IsRequired();
			builder.HasOne(c => c.User)
				.WithMany(u => u.CheckIns)
				.HasForeignKey(c => c.UserId)
				.OnDelete(DeleteBehavior.Cascade);
			builder.HasOne(c => c.Response)
				.WithMany()
				.HasForeignKey(c => c.ResponseId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(c => new { c.UserId, c.ScheduledFor });
			builder.HasIndex(c => c.ScheduledFor)
				.HasFilter("[TimeReceived] IS NULL");
		}
	}
}
