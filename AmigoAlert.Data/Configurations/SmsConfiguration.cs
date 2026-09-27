using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the Sms entity in the database.
	/// </summary>
	internal class SmsConfiguration : IEntityTypeConfiguration<Sms>
	{
		public void Configure(EntityTypeBuilder<Sms> builder)
		{
			builder.ToTable("Sms");
			builder.HasKey(s => s.Id);
			builder.Property(s => s.Id).ValueGeneratedOnAdd();
			builder.Property(s => s.TimeSent).IsRequired();
			builder.Property(s => s.TwilioSid)
				.HasMaxLength(34)
				.IsUnicode(false)
				.IsFixedLength();
			builder.Property(s => s.Status).HasMaxLength(20);
			builder.HasOne(s => s.EmergencyContact)
				.WithMany(e => e.SmsMessages)
				.HasForeignKey(s => s.EmergencyContactId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasOne(s => s.StatusLog)
				.WithMany(s => s.Sms)
				.HasForeignKey(s => s.StatusLogId)
				.OnDelete(DeleteBehavior.SetNull);
			builder.HasOne(s => s.MessageType)
				.WithMany()
				.HasForeignKey(s => s.MessageTypeId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(s => s.TwilioSid);
		}
	}
}
