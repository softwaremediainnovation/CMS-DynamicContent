using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoModerationColumnsToVoteLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                table: "DynamicContentVoteLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReviewReason",
                table: "DynamicContentVoteLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "DynamicContentVoteLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationMode",
                table: "DynamicContentVoteLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DynamicContentVoteLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "TakenDownAt",
                table: "DynamicContentVoteLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicContentVoteLogs_DynamicContentMasterId_Status",
                table: "DynamicContentVoteLogs",
                columns: new[] { "DynamicContentMasterId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DynamicContentVoteLogs_DynamicContentMasterId_Status",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "AiReviewReason",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "ModerationMode",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DynamicContentVoteLogs");

            migrationBuilder.DropColumn(
                name: "TakenDownAt",
                table: "DynamicContentVoteLogs");
        }
    }
}
