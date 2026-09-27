using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data
{
	/// <summary>
	/// Represents a factory for creating instances of the AmigoAlertDbContext at design time.
	/// </summary>
	internal class AmigoAlertDbContextFactory : IDesignTimeDbContextFactory<AmigoAlertDbContext>
	{
		public AmigoAlertDbContext CreateDbContext(string[] args)
		{
			var options = new DbContextOptionsBuilder<AmigoAlertDbContext>()
				.UseSqlServer("Server=amigoalert-db-server.database.windows.net;Database=AmigoAlertDB;" +
				"Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;",
				o => o.UseNetTopologySuite())
				.Options;

			return new AmigoAlertDbContext(options);
		}
	}
}
