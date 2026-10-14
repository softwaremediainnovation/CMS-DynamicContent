using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoCommentTolakAnginSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "samsung-zfold5",
                column: "GameEntryFile",
                value: "index.html");

            migrationBuilder.InsertData(
                table: "DynamicContentMasters",
                columns: new[] { "Id", "AdHeadline", "AdSubheadline", "BannerImageUrl", "BrandLogoUrl", "CampaignTitle", "ClientName", "ContentType", "CreatedAt", "GameCustomParams", "GameEntryFile", "GameFolder", "GapDurationMinutes", "InstructionComment", "IsActive", "LocationTagDetect", "ParticipationMode", "ThemeColorHex", "WelcomeMessage" },
                values: new object[] { "tolak-angin-live", "Orang Pintar Minum Tolak Angin", "Kirimkan foto selfie & ucapan hangatmu sekarang ke layar Videotron!", "/images/tolak-angin-banner.jpg", "/images/tolak-angin-logo.png", "Tolak Angin — Live Greetings & Photo", "PT Industri Jamu dan Farmasi Sido Muncul Tbk", "PhotoComment", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "index.html", null, 15, "Posisikan wajah Anda di siluet panduan, lalu kirimkan ucapan terbaikmu!", true, true, "multipletime", "#D97706", "Selamat Datang di Live Greetings & Photo Booth Tolak Angin!" });

            migrationBuilder.InsertData(
                table: "DynamicContentQuestions",
                columns: new[] { "Id", "CorrectOptionKey", "DynamicContentMasterId", "QuestionImageUrl", "QuestionText", "QuestionType", "SortOrder" },
                values: new object[] { 2, null, "tolak-angin-live", null, "Live Photo & Greetings", "photo", 1 });

            migrationBuilder.InsertData(
                table: "DynamicContentOptions",
                columns: new[] { "Id", "ColorHex", "IsCorrect", "OptionImageUrl", "OptionKey", "OptionText", "QuestionId", "SortOrder", "VoteCount" },
                values: new object[] { 5, "#D97706", false, null, "PHOTO", "Foto Partisipan", 2, 1, 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DynamicContentOptions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "DynamicContentQuestions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "tolak-angin-live");

            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "samsung-zfold5",
                column: "GameEntryFile",
                value: null);
        }
    }
}
