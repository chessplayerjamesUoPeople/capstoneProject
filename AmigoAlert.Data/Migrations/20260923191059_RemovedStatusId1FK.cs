using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmigoAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedStatusId1FK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_Status_StatusId1",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_StatusId1",
                table: "User");

            migrationBuilder.DropColumn(
                name: "StatusId1",
                table: "User");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "StatusId1",
                table: "User",
                type: "tinyint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_StatusId1",
                table: "User",
                column: "StatusId1");

            migrationBuilder.AddForeignKey(
                name: "FK_User_Status_StatusId1",
                table: "User",
                column: "StatusId1",
                principalTable: "Status",
                principalColumn: "Id");
        }
    }
}
