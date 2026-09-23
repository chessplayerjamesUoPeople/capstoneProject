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
	/// Represents the configuration settings for the Status entity in the database.
	/// </summary>
	internal class StatusConfiguration : IEntityTypeConfiguration<Status>
	{
		public void Configure(EntityTypeBuilder<Status> builder)
		{
			builder.ToTable("Status");
			builder.HasKey(s => s.Id);
			builder.Property(s => s.Id).ValueGeneratedNever();
			builder.Property(s => s.Name)
				.IsRequired()
				.HasMaxLength(25);
			builder.HasIndex(s => s.Name)
				.IsUnique();
			builder.HasData(
				Enum.GetValues<StatusType>()
				.Select(s => new Status
				{
					Id = (byte)s,
					Name = s.ToString()
				}));
		}
	}
}
