using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the EmergencyContact entity in the database.
	/// </summary>
	internal class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
	{
		public void Configure(EntityTypeBuilder<EmergencyContact> builder)
		{
			builder.ToTable("EmergencyContact");
			builder.HasKey(e => e.Id);
			builder.Property(e => e.Id).ValueGeneratedNever();
			builder.Property(e => e.Name)
				.HasMaxLength(50)
				.IsRequired();
			builder.Property(e => e.Phone)
				.HasMaxLength(20)
				.IsRequired();
			builder.Property(e => e.ConsentRequestedAt).IsRequired();
			builder.Property(e => e.IsActive).IsRequired();
			builder.HasOne(e => e.User)
				.WithMany(u => u.EmergencyContacts)
				.HasForeignKey(e => e.UserId)
				.OnDelete(DeleteBehavior.Cascade);
			builder.HasOne(e => e.ConsentStatus)
				.WithMany(c => c.EmergencyContacts)
				.HasForeignKey(e => e.ConsentStatusId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(e => e.UserId)
				.IsUnique()
				.HasFilter("[IsActive] = 1");
			builder.HasIndex(e => e.ConsentToken).IsUnique();
		}
	}
}
