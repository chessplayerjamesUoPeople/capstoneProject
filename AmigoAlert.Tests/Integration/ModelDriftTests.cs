using AmigoAlert.Data;
using AmigoAlert.Shared.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AmigoAlert.Tests.Integration
{
	/// <summary>
	/// I-16 and I-17: the migration chain builds the schema from nothing, and the EF model matches the latest migration's snapshot. The
	/// second catches snapshot drift and accidental shadow properties, such as the <c>StatusId1</c> foreign key a stray navigation once
	/// produced, which are invisible in the C# and obvious here.
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public sealed class ModelDriftTests(IntegrationApiFactory factory)
	{
		[Fact]
		public async Task I16_AllMigrationsApplyToAnEmptyDatabase()
		{
			// A throwaway database beside the test one, so the shared database's state is never in question.
			SqlConnectionStringBuilder connection = new SqlConnectionStringBuilder(factory.ConnectionString);
			connection.InitialCatalog = $"{connection.InitialCatalog}_Migrations_{Guid.NewGuid():N}";
			await using AmigoAlertDbContext db = NewContext(connection.ConnectionString);

			try
			{
				await db.Database.MigrateAsync();

				Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
				Assert.Equal(Enum.GetValues<StatusType>().Length, await db.Statuses.CountAsync());
			}
			finally
			{
				await db.Database.EnsureDeletedAsync();
			}
		}

		[Fact]
		public void I17_ModelMatchesTheLatestSnapshot()
		{
			using AmigoAlertDbContext db = NewContext(factory.ConnectionString);

			// The comparison `dotnet ef migrations add` makes before deciding there is anything to scaffold. Needs no connection.
			if (!db.Database.HasPendingModelChanges())
			{
				return;
			}

			// Repeats it through the differ, only to name what drifted. The check above has already finalized the snapshot model.
			IModel snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model);
			IReadOnlyList<MigrationOperation> differences = db.GetService<IMigrationsModelDiffer>()
				.GetDifferences(snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

			Assert.Fail("The model differs from the latest migration's snapshot; add a migration or remove the change. Differences: " +
				string.Join("; ", differences.Select(Describe)));
		}

		private static AmigoAlertDbContext NewContext(string connectionString)
		{
			return new AmigoAlertDbContext(new DbContextOptionsBuilder<AmigoAlertDbContext>()
				.UseSqlServer(connectionString, sql => sql.UseNetTopologySuite())
				.Options);
		}

		private static string Describe(MigrationOperation operation)
		{
			string kind = operation.GetType().Name.Replace("Operation", "");

			return operation switch
			{
				ColumnOperation column => $"{kind} {column.Table}.{column.Name}",
				DropColumnOperation column => $"{kind} {column.Table}.{column.Name}",
				ITableMigrationOperation table => $"{kind} on {table.Table}",
				_ => kind
			};
		}
	}
}
