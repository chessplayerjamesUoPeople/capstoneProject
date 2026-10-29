using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration settings for the EmailVerification entity in the database.
	/// </summary>
	internal class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
	{
		public void Configure(EntityTypeBuilder<EmailVerification> builder)
		{
			builder.ToTable("EmailVerification");
			builder.HasKey(e => e.Id);
			builder.Property(e => e.Id).ValueGeneratedOnAdd();
			builder.Property(e => e.Email)
				.HasMaxLength(100)
				.IsRequired();
			builder.Property(e => e.CodeHash)
				.HasMaxLength(32)
				.IsFixedLength()
				.IsRequired();
			builder.Property(e => e.CodeSalt)
				.HasMaxLength(16)
				.IsFixedLength()
				.IsRequired();
			builder.Property(e => e.CreatedAt).IsRequired();
			builder.Property(e => e.ExpiresAt).IsRequired();
			builder.HasOne(e => e.Purpose)
				.WithMany()
				.HasForeignKey(e => e.PurposeId)
				.OnDelete(DeleteBehavior.Restrict);
			// Every lookup is by address and purpose, newest first.
			builder.HasIndex(e => new { e.Email, e.PurposeId, e.CreatedAt });
		}
	}
}
