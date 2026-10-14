using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddBossRevisionParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SurveyVoteLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LocationDetected",
                table: "SurveyVoteLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SurveyVoteLogs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "SurveyVoteLogs",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "SurveyMasters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GapDurationMinutes",
                table: "SurveyMasters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InstructionComment",
                table: "SurveyMasters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LocationTagDetect",
                table: "SurveyMasters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParticipationMode",
                table: "SurveyMasters",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessage",
                table: "SurveyMasters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SurveyMasters",
                keyColumn: "Id",
                keyValue: "samsung-zfold5",
                columns: new[] { "ContentType", "GapDurationMinutes", "InstructionComment", "LocationTagDetect", "ParticipationMode", "WelcomeMessage" },
                values: new object[] { "Survey", 10, "Silakan isi nama dan nomor telepon untuk berpartisipasi", false, "onetime", "Selamat Datang di Interaktif DOOH Warna Warni" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "LocationDetected",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "SurveyVoteLogs");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "SurveyMasters");

            migrationBuilder.DropColumn(
                name: "GapDurationMinutes",
                table: "SurveyMasters");

            migrationBuilder.DropColumn(
                name: "InstructionComment",
                table: "SurveyMasters");

            migrationBuilder.DropColumn(
                name: "LocationTagDetect",
                table: "SurveyMasters");

            migrationBuilder.DropColumn(
                name: "ParticipationMode",
                table: "SurveyMasters");

            migrationBuilder.DropColumn(
                name: "WelcomeMessage",
                table: "SurveyMasters");
        }
    }
}
