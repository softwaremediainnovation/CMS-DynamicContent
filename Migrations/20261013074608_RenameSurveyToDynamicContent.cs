using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class RenameSurveyToDynamicContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyOptions");

            migrationBuilder.DropTable(
                name: "SurveyVoteLogs");

            migrationBuilder.DropTable(
                name: "SurveyQuestions");

            migrationBuilder.DropTable(
                name: "SurveyMasters");

            migrationBuilder.CreateTable(
                name: "DynamicContentMasters",
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
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParticipationMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GapDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    LocationTagDetect = table.Column<bool>(type: "bit", nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InstructionComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicContentMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DynamicContentQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DynamicContentMasterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuestionImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectOptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicContentQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicContentQuestions_DynamicContentMasters_DynamicContentMasterId",
                        column: x => x.DynamicContentMasterId,
                        principalTable: "DynamicContentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DynamicContentOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OptionImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicContentOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicContentOptions_DynamicContentQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "DynamicContentQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DynamicContentVoteLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    DynamicContentMasterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TextResponse = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SiteId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceBrand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BrowserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScreenResolution = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UserLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimeToVoteSeconds = table.Column<double>(type: "float", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    LocationDetected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicContentVoteLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicContentVoteLogs_DynamicContentQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "DynamicContentQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "DynamicContentMasters",
                columns: new[] { "Id", "AdHeadline", "AdSubheadline", "BannerImageUrl", "BrandLogoUrl", "CampaignTitle", "ClientName", "ContentType", "CreatedAt", "GapDurationMinutes", "InstructionComment", "IsActive", "LocationTagDetect", "ParticipationMode", "ThemeColorHex", "WelcomeMessage" },
                values: new object[] { "samsung-zfold5", "REVOLUSI LAYAR LIPAT", "SAMSUNG GALAXY Z FOLD5", "/images/samsung_galaxy_fold.svg", null, "SURVEY PENGGUNA SAMSUNG MOBILE", "Samsung", "Survey", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10, "Silakan isi nama dan nomor telepon untuk berpartisipasi", true, false, "onetime", "#0072FF", "Selamat Datang di Interaktif DOOH Warna Warni" });

            migrationBuilder.InsertData(
                table: "DynamicContentQuestions",
                columns: new[] { "Id", "CorrectOptionKey", "DynamicContentMasterId", "QuestionImageUrl", "QuestionText", "QuestionType", "SortOrder" },
                values: new object[] { 1, null, "samsung-zfold5", null, "Manakah fitur Samsung yang PALING ANDA SUKAI?", "multiple", 1 });

            migrationBuilder.InsertData(
                table: "DynamicContentOptions",
                columns: new[] { "Id", "ColorHex", "IsCorrect", "OptionImageUrl", "OptionKey", "OptionText", "QuestionId", "SortOrder", "VoteCount" },
                values: new object[,]
                {
                    { 1, "#3B82F6", false, null, "A", "Baterai Tahan Lama", 1, 1, 47 },
                    { 2, "#10B981", false, null, "B", "Kamera Tajam", 1, 2, 33 },
                    { 3, "#F59E0B", false, null, "C", "Anti Air IPX8", 1, 3, 10 },
                    { 4, "#8B5CF6", false, null, "D", "AI Image Editing", 1, 4, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentOptions_QuestionId_OptionKey",
                table: "DynamicContentOptions",
                columns: new[] { "QuestionId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentQuestions_DynamicContentMasterId",
                table: "DynamicContentQuestions",
                column: "DynamicContentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentVoteLogs_DynamicContentMasterId_City",
                table: "DynamicContentVoteLogs",
                columns: new[] { "DynamicContentMasterId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentVoteLogs_DynamicContentMasterId_DeviceFingerprint",
                table: "DynamicContentVoteLogs",
                columns: new[] { "DynamicContentMasterId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentVoteLogs_DynamicContentMasterId_SiteId",
                table: "DynamicContentVoteLogs",
                columns: new[] { "DynamicContentMasterId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentVoteLogs_QuestionId_OptionKey",
                table: "DynamicContentVoteLogs",
                columns: new[] { "QuestionId", "OptionKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicContentOptions");

            migrationBuilder.DropTable(
                name: "DynamicContentVoteLogs");

            migrationBuilder.DropTable(
                name: "DynamicContentQuestions");

            migrationBuilder.DropTable(
                name: "DynamicContentMasters");

            migrationBuilder.CreateTable(
                name: "SurveyMasters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AdHeadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AdSubheadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BannerImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CampaignTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GapDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    InstructionComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LocationTagDetect = table.Column<bool>(type: "bit", nullable: false),
                    ParticipationMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ThemeColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
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
                    QuestionImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SurveyOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OptionImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyOptions_SurveyQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurveyVoteLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    BrowserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceBrand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DeviceModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    LocationDetected = table.Column<bool>(type: "bit", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    OperatingSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OptionKey = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScreenResolution = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SiteId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SurveyMasterId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TextResponse = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TimeToVoteSeconds = table.Column<double>(type: "float", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyVoteLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyVoteLogs_SurveyQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "SurveyQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SurveyMasters",
                columns: new[] { "Id", "AdHeadline", "AdSubheadline", "BannerImageUrl", "BrandLogoUrl", "CampaignTitle", "ClientName", "ContentType", "CreatedAt", "GapDurationMinutes", "InstructionComment", "IsActive", "LocationTagDetect", "ParticipationMode", "ThemeColorHex", "WelcomeMessage" },
                values: new object[] { "samsung-zfold5", "REVOLUSI LAYAR LIPAT", "SAMSUNG GALAXY Z FOLD5", "/images/samsung_galaxy_fold.svg", null, "SURVEY PENGGUNA SAMSUNG MOBILE", "Samsung", "Survey", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10, "Silakan isi nama dan nomor telepon untuk berpartisipasi", true, false, "onetime", "#0072FF", "Selamat Datang di Interaktif DOOH Warna Warni" });

            migrationBuilder.InsertData(
                table: "SurveyQuestions",
                columns: new[] { "Id", "QuestionImageUrl", "QuestionText", "QuestionType", "SortOrder", "SurveyMasterId" },
                values: new object[] { 1, null, "Manakah fitur Samsung yang PALING ANDA SUKAI?", "MultipleChoice", 1, "samsung-zfold5" });

            migrationBuilder.InsertData(
                table: "SurveyOptions",
                columns: new[] { "Id", "ColorHex", "OptionImageUrl", "OptionKey", "OptionText", "QuestionId", "SortOrder", "VoteCount" },
                values: new object[,]
                {
                    { 1, "#3B82F6", null, "A", "Baterai Tahan Lama", 1, 1, 47 },
                    { 2, "#10B981", null, "B", "Kamera Tajam", 1, 2, 33 },
                    { 3, "#F59E0B", null, "C", "Anti Air IPX8", 1, 3, 10 },
                    { 4, "#8B5CF6", null, "D", "AI Image Editing", 1, 4, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptions_QuestionId_OptionKey",
                table: "SurveyOptions",
                columns: new[] { "QuestionId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestions_SurveyMasterId",
                table: "SurveyQuestions",
                column: "SurveyMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_QuestionId_OptionKey",
                table: "SurveyVoteLogs",
                columns: new[] { "QuestionId", "OptionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_City",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_DeviceFingerprint",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "DeviceFingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_SiteId",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "SiteId" });
        }
    }
}
