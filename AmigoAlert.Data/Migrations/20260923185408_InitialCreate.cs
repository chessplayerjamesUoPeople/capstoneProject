using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AmigoAlert.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckInResponse",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckInResponse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentStatus",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsMessageTypes",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessageTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Landmarks = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    AcceptedTermsTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedTermsVersion = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Notification1Time = table.Column<TimeOnly>(type: "time", nullable: true),
                    Notification2Time = table.Column<TimeOnly>(type: "time", nullable: true),
                    StatusId1 = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Status_StatusId",
                        column: x => x.StatusId,
                        principalTable: "Status",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_User_Status_StatusId1",
                        column: x => x.StatusId1,
                        principalTable: "Status",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CheckIn",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeSent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TimeReceived = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseId = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIn", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIn_CheckInResponse_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "CheckInResponse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckIn_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyContact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConsentStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    ConsentToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsentTokenExpires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsentRequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsentRespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyContact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmergencyContact_ConsentStatus_ConsentStatusId",
                        column: x => x.ConsentStatusId,
                        principalTable: "ConsentStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmergencyContact_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeOccurrence = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusLog_Status_StatusId",
                        column: x => x.StatusId,
                        principalTable: "Status",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatusLog_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmergencyContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusLogId = table.Column<long>(type: "bigint", nullable: true),
                    TimeSent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TwilioSid = table.Column<string>(type: "char(34)", unicode: false, fixedLength: true, maxLength: 34, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ErrorCode = table.Column<int>(type: "int", nullable: true),
                    MessageTypeId = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sms_EmergencyContact_EmergencyContactId",
                        column: x => x.EmergencyContactId,
                        principalTable: "EmergencyContact",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sms_SmsMessageTypes_MessageTypeId",
                        column: x => x.MessageTypeId,
                        principalTable: "SmsMessageTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sms_StatusLog_StatusLogId",
                        column: x => x.StatusLogId,
                        principalTable: "StatusLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "CheckInResponse",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Okay" },
                    { (byte)2, "Emergency" },
                    { (byte)3, "NoResponse" }
                });

            migrationBuilder.InsertData(
                table: "ConsentStatus",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Pending" },
                    { (byte)2, "Granted" },
                    { (byte)3, "Rejected" },
                    { (byte)4, "Expired" },
                    { (byte)5, "Revoked" }
                });

            migrationBuilder.InsertData(
                table: "Status",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Okay" },
                    { (byte)2, "Pending1" },
                    { (byte)3, "Pending2" },
                    { (byte)4, "Escalated" },
                    { (byte)5, "Emergency" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIn_ResponseId",
                table: "CheckIn",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIn_ScheduledFor",
                table: "CheckIn",
                column: "ScheduledFor",
                filter: "[TimeReceived] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIn_UserId_ScheduledFor",
                table: "CheckIn",
                columns: new[] { "UserId", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckInResponse_Name",
                table: "CheckInResponse",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentStatus_Name",
                table: "ConsentStatus",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContact_ConsentStatusId",
                table: "EmergencyContact",
                column: "ConsentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContact_ConsentToken",
                table: "EmergencyContact",
                column: "ConsentToken",
                unique: true,
                filter: "[ConsentToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContact_UserId",
                table: "EmergencyContact",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_EmergencyContactId",
                table: "Sms",
                column: "EmergencyContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_MessageTypeId",
                table: "Sms",
                column: "MessageTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_StatusLogId",
                table: "Sms",
                column: "StatusLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Sms_TwilioSid",
                table: "Sms",
                column: "TwilioSid");

            migrationBuilder.CreateIndex(
                name: "IX_Status_Name",
                table: "Status",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusLog_StatusId",
                table: "StatusLog",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusLog_UserId_TimeOccurrence",
                table: "StatusLog",
                columns: new[] { "UserId", "TimeOccurrence" });

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                table: "User",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_StatusId",
                table: "User",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_User_StatusId1",
                table: "User",
                column: "StatusId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckIn");

            migrationBuilder.DropTable(
                name: "Sms");

            migrationBuilder.DropTable(
                name: "CheckInResponse");

            migrationBuilder.DropTable(
                name: "EmergencyContact");

            migrationBuilder.DropTable(
                name: "SmsMessageTypes");

            migrationBuilder.DropTable(
                name: "StatusLog");

            migrationBuilder.DropTable(
                name: "ConsentStatus");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Status");
        }
    }
}
