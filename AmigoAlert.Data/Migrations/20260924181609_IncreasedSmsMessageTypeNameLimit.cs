using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AmigoAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncreasedSmsMessageTypeNameLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SmsMessageTypes",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.InsertData(
                table: "SmsMessageTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { (byte)1, "ConsentRequest" },
                    { (byte)2, "MissedCheckInEscalation" },
                    { (byte)3, "EmergencyActivation" },
                    { (byte)4, "EmergencyCancelled" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessageTypes_Name",
                table: "SmsMessageTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsMessageTypes_Name",
                table: "SmsMessageTypes");

            migrationBuilder.DeleteData(
                table: "SmsMessageTypes",
                keyColumn: "Id",
                keyValue: (byte)1);

            migrationBuilder.DeleteData(
                table: "SmsMessageTypes",
                keyColumn: "Id",
                keyValue: (byte)2);

            migrationBuilder.DeleteData(
                table: "SmsMessageTypes",
                keyColumn: "Id",
                keyValue: (byte)3);

            migrationBuilder.DeleteData(
                table: "SmsMessageTypes",
                keyColumn: "Id",
                keyValue: (byte)4);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SmsMessageTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(25)",
                oldMaxLength: 25);
        }
    }
}
