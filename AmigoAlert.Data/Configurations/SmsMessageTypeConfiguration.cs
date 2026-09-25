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
	/// Represents the configuration for the SmsMessageType entity.
	/// </summary>
	internal class SmsMessageTypeConfiguration : IEntityTypeConfiguration<SmsMessageType>
	{
		public void Configure(EntityTypeBuilder<SmsMessageType> builder)
		{
			builder.ToTable("SmsMessageTypes");
			builder.HasKey(s => s.Id);
			builder.Property(s => s.Id).ValueGeneratedNever();
			builder.Property(s => s.Name)
				.IsRequired()
				.HasMaxLength(25);
			builder.HasIndex(s => s.Name).IsUnique();
			builder.HasData(
				Enum.GetValues<SmsMessageTypeEnum>()
				.Select(s => new SmsMessageType
				{
					Id = (byte)s,
					Name = s.ToString()
				}));
		}
	}
}
