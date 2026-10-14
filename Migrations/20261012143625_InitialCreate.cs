using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Surveys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdHeadline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdSubheadline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdVideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyVoteLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SiteId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyVoteLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyOptions_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Surveys",
                columns: new[] { "Id", "AdHeadline", "AdImageUrl", "AdSubheadline", "AdVideoUrl", "BrandName", "CreatedAt", "IsActive", "Question", "Title" },
                values: new object[] { "samsung-zfold5", "REVOLUSI LAYAR LIPAT", "/images/samsung_galaxy_fold.png", "SAMSUNG GALAXY Z FOLD5", null, "Samsung", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Manakah fitur Samsung yang PALING ANDA SUKAI?", "SURVEY PENGGUNA SAMSUNG MOBILE" });

            migrationBuilder.InsertData(
                table: "SurveyOptions",
                columns: new[] { "Id", "ColorHex", "OptionKey", "OptionText", "SortOrder", "SurveyId", "VoteCount" },
                values: new object[,]
                {
                    { 1, "#3B82F6", "A", "Baterai Tahan Lama", 1, "samsung-zfold5", 47 },
                    { 2, "#10B981", "B", "Kamera Tajam", 2, "samsung-zfold5", 33 },
                    { 3, "#F59E0B", "C", "Anti Air IPX8", 3, "samsung-zfold5", 10 },
                    { 4, "#8B5CF6", "D", "AI Image Editing", 4, "samsung-zfold5", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptions_SurveyId_OptionKey",
                table: "SurveyOptions",
                columns: new[] { "SurveyId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyId_DeviceFingerprint",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyId_SiteId",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyId", "SiteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyOptions");

            migrationBuilder.DropTable(
                name: "SurveyVoteLogs");

            migrationBuilder.DropTable(
                name: "Surveys");
        }
    }
}
