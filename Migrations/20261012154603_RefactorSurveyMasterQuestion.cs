using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSurveyMasterQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyOptions_Surveys_SurveyId",
                table: "SurveyOptions");

            migrationBuilder.DropTable(
                name: "Surveys");

            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_SurveyId_DeviceFingerprint",
                table: "SurveyVoteLogs");

            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_SurveyId_SiteId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropIndex(
                name: "IX_SurveyOptions_SurveyId_OptionKey",
                table: "SurveyOptions");

            migrationBuilder.DropColumn(
                name: "SurveyId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "SurveyId",
                table: "SurveyOptions");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "SurveyVoteLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionId",
                table: "SurveyVoteLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SurveyMasterId",
                table: "SurveyVoteLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextResponse",
                table: "SurveyVoteLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionImageUrl",
                table: "SurveyOptions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionId",
                table: "SurveyOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SurveyMasters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CampaignTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AdHeadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdSubheadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BannerImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThemeColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyMasterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuestionImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestions_SurveyMasters_SurveyMasterId",
                        column: x => x.SurveyMasterId,
                        principalTable: "SurveyMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SurveyMasters",
                columns: new[] { "Id", "AdHeadline", "AdSubheadline", "BannerImageUrl", "BrandLogoUrl", "CampaignTitle", "ClientName", "CreatedAt", "IsActive", "ThemeColorHex" },
                values: new object[] { "samsung-zfold5", "REVOLUSI LAYAR LIPAT", "SAMSUNG GALAXY Z FOLD5", "/images/samsung_galaxy_fold.svg", null, "SURVEY PENGGUNA SAMSUNG MOBILE", "Samsung", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "#0072FF" });

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "OptionImageUrl", "QuestionId" },
                values: new object[] { null, 1 });

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "OptionImageUrl", "QuestionId" },
                values: new object[] { null, 1 });

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "OptionImageUrl", "QuestionId" },
                values: new object[] { null, 1 });

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "OptionImageUrl", "QuestionId" },
                values: new object[] { null, 1 });

            migrationBuilder.InsertData(
                table: "SurveyQuestions",
                columns: new[] { "Id", "QuestionImageUrl", "QuestionText", "QuestionType", "SortOrder", "SurveyMasterId" },
                values: new object[] { 1, null, "Manakah fitur Samsung yang PALING ANDA SUKAI?", "MultipleChoice", 1, "samsung-zfold5" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_QuestionId_OptionKey",
                table: "SurveyVoteLogs",
                columns: new[] { "QuestionId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_DeviceFingerprint",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_SiteId",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptions_QuestionId_OptionKey",
                table: "SurveyOptions",
                columns: new[] { "QuestionId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestions_SurveyMasterId",
                table: "SurveyQuestions",
                column: "SurveyMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyOptions_SurveyQuestions_QuestionId",
                table: "SurveyOptions",
                column: "QuestionId",
                principalTable: "SurveyQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyVoteLogs_SurveyQuestions_QuestionId",
                table: "SurveyVoteLogs",
                column: "QuestionId",
                principalTable: "SurveyQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyOptions_SurveyQuestions_QuestionId",
                table: "SurveyOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyVoteLogs_SurveyQuestions_QuestionId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropTable(
                name: "SurveyQuestions");

            migrationBuilder.DropTable(
                name: "SurveyMasters");

            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_QuestionId_OptionKey",
                table: "SurveyVoteLogs");

            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_DeviceFingerprint",
                table: "SurveyVoteLogs");

            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_SiteId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropIndex(
                name: "IX_SurveyOptions_QuestionId_OptionKey",
                table: "SurveyOptions");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "SurveyMasterId",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "TextResponse",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "OptionImageUrl",
                table: "SurveyOptions");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "SurveyOptions");

            migrationBuilder.AddColumn<string>(
                name: "SurveyId",
                table: "SurveyVoteLogs",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SurveyId",
                table: "SurveyOptions",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Surveys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AdHeadline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdSubheadline = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdVideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surveys", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 1,
                column: "SurveyId",
                value: "samsung-zfold5");

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 2,
                column: "SurveyId",
                value: "samsung-zfold5");

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 3,
                column: "SurveyId",
                value: "samsung-zfold5");

            migrationBuilder.UpdateData(
                table: "SurveyOptions",
                keyColumn: "Id",
                keyValue: 4,
                column: "SurveyId",
                value: "samsung-zfold5");

            migrationBuilder.InsertData(
                table: "Surveys",
                columns: new[] { "Id", "AdHeadline", "AdImageUrl", "AdSubheadline", "AdVideoUrl", "BrandName", "CreatedAt", "IsActive", "Question", "Title" },
                values: new object[] { "samsung-zfold5", "REVOLUSI LAYAR LIPAT", "/images/samsung_galaxy_fold.png", "SAMSUNG GALAXY Z FOLD5", null, "Samsung", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Manakah fitur Samsung yang PALING ANDA SUKAI?", "SURVEY PENGGUNA SAMSUNG MOBILE" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyId_DeviceFingerprint",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyId_SiteId",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptions_SurveyId_OptionKey",
                table: "SurveyOptions",
                columns: new[] { "SurveyId", "OptionKey" });

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyOptions_Surveys_SurveyId",
                table: "SurveyOptions",
                column: "SurveyId",
                principalTable: "Surveys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
