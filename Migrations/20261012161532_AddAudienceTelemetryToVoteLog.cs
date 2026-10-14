using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceTelemetryToVoteLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                table: "SurveyVoteLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "SurveyVoteLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceBrand",
                table: "SurveyVoteLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceModel",
                table: "SurveyVoteLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "SurveyVoteLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "SurveyVoteLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreenResolution",
                table: "SurveyVoteLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TimeToVoteSeconds",
                table: "SurveyVoteLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "SurveyVoteLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserLanguage",
                table: "SurveyVoteLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_City",
                table: "SurveyVoteLogs",
                columns: new[] { "SurveyMasterId", "City" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SurveyVoteLogs_SurveyMasterId_City",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "BrowserName",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "City",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "DeviceBrand",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "DeviceModel",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "ScreenResolution",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "TimeToVoteSeconds",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "UserLanguage",
                table: "SurveyVoteLogs");
        }
    }
}
