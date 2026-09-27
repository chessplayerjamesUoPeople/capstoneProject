using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the User entity in the database.
	/// </summary>
	internal class UserConfiguration : IEntityTypeConfiguration<User>
	{
		public void Configure(EntityTypeBuilder<User> builder)
		{
			builder.ToTable("User");
			builder.HasKey(u => u.Id);
			builder.Property(u => u.Id).ValueGeneratedNever();
			builder.Property(u => u.Name)
				.HasMaxLength(50)
				.IsRequired();
			builder.Property(u => u.Email)
				.HasMaxLength(100)
				.IsRequired();
			builder.Property(u => u.Phone).HasMaxLength(20);
			builder.Property(u => u.Address)
				.HasMaxLength(200)
				.IsRequired();
			builder.Property(u => u.Landmarks).HasMaxLength(150);
			builder.Property(u => u.AcceptedTermsTime).IsRequired();
			builder.Property(u => u.AcceptedTermsVersion)
				.HasMaxLength(10)
				.IsRequired();
			builder.HasOne(u => u.Status)
				.WithMany()
				.HasForeignKey(u => u.StatusId)
				.OnDelete(DeleteBehavior.Restrict);
			builder.HasIndex(u => u.Email).IsUnique();
		}
	}
}
