using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddPetSubmissionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetSubmissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Room = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ModerationMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AiReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AiConfidence = table.Column<double>(type: "float", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TakenDownAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetSubmissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PetSubmissions_CreatedAt",
                table: "PetSubmissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PetSubmissions_DeviceFingerprint",
                table: "PetSubmissions",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_PetSubmissions_Room_Status",
                table: "PetSubmissions",
                columns: new[] { "Room", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetSubmissions");
        }
    }
}
