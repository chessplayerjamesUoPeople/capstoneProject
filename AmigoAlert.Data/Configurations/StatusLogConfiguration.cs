using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the StatusLog entity in the database.
	/// </summary>
	internal class StatusLogConfiguration : IEntityTypeConfiguration<StatusLog>
	{
		public void Configure(EntityTypeBuilder<StatusLog> builder)
		{
			builder.ToTable("StatusLog");
			builder.HasKey(s => s.Id);
			builder.Property(s => s.Id).ValueGeneratedOnAdd();
			builder.Property(s => s.TimeOccurrence).IsRequired();
			builder.HasOne(s => s.User)
				.WithMany(u => u.StatusLogs)
				.HasForeignKey(s => s.UserId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasOne(s => s.Status)
				.WithMany()
				.HasForeignKey(s => s.StatusId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(s => new { s.UserId, s.TimeOccurrence });
		}
	}
}
