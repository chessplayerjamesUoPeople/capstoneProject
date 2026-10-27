using AmigoAlert.Data.Entities;
using AmigoAlert.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration for the VerificationPurpose entity.
	/// </summary>
	internal class VerificationPurposeConfiguration : IEntityTypeConfiguration<VerificationPurpose>
	{
		public void Configure(EntityTypeBuilder<VerificationPurpose> builder)
		{
			builder.ToTable("VerificationPurpose");
			builder.HasKey(v => v.Id);
			builder.Property(v => v.Id).ValueGeneratedNever();
			builder.Property(v => v.Name)
				.HasMaxLength(25)
				.IsRequired();
			builder.HasIndex(v => v.Name).IsUnique();
			builder.HasData(
				Enum.GetValues<VerificationPurposeType>()
				.Select(v => new VerificationPurpose
				{
					Id = (byte)v,
					Name = v.ToString()
				}));
		}
	}
}
