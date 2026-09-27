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
	/// Represents the configuration for the CheckInResponse entity in the database context.
	/// </summary>
	internal class CheckInResponseConfiguration : IEntityTypeConfiguration<CheckInResponse>
	{
		public void Configure(EntityTypeBuilder<CheckInResponse> builder)
		{
			builder.ToTable("CheckInResponse");
			builder.HasKey(r => r.Id);
			builder.Property(r => r.Id).ValueGeneratedNever();
			builder.Property(r => r.Name)
				.HasMaxLength(25)
				.IsRequired();
			builder.HasIndex(r => r.Name).IsUnique();
			builder.HasData(
				Enum.GetValues<CheckInResponseType>()
				.Select(r => new CheckInResponse
				{
					Id = (byte)r,
					Name = r.ToString()
				}));
		}
	}
}
