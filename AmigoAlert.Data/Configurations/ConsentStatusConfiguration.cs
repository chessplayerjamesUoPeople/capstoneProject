using AmigoAlert.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data.Configurations
{
	/// <summary>
	/// Represents the configuration for the ConsentStatus entity in the database context.
	/// </summary>
	internal class ConsentStatusConfiguration : IEntityTypeConfiguration<ConsentStatus>
	{
		public void Configure(EntityTypeBuilder<ConsentStatus> builder)
		{
			builder.ToTable("ConsentStatus");
			builder.HasKey(c => c.Id);
			builder.Property(c => c.Id).ValueGeneratedNever();
			builder.Property(c => c.Name)
				.HasMaxLength(25)
				.IsRequired();
			builder.HasIndex(c => c.Name).IsUnique();
			builder.HasData(
				Enum.GetValues<Shared.Enums.ConsentStatusType>()
				.Select(c => new ConsentStatus
				{
					Id = (byte)c,
					Name = c.ToString()
				}));
		}
	}
}
