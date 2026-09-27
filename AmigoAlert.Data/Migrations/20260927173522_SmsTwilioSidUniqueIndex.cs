using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmigoAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class SmsTwilioSidUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sms_TwilioSid",
                table: "Sms");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_TwilioSid",
                table: "Sms",
                column: "TwilioSid",
                unique: true,
                filter: "[TwilioSid] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sms_TwilioSid",
                table: "Sms");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_TwilioSid",
                table: "Sms",
                column: "TwilioSid");
        }
    }
}
