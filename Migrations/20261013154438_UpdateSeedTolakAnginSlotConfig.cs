using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedTolakAnginSlotConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "tolak-angin-live",
                column: "SlotConfigJson",
                value: "{\"DetectionMode\":\"auto\",\"FaceX\":77.08,\"FaceY\":1.79,\"FaceWidth\":13.33,\"FaceHeight\":39.29,\"FaceShape\":\"ellipse\",\"SpeechX\":49.0,\"SpeechY\":66.43,\"SpeechWidth\":48.33,\"SpeechHeight\":28.93,\"SpeechBgColor\":\"#FEE87D\",\"SpeechTextColor\":\"#1E293B\",\"CollarOverlayUrl\":\"/images/tolak_endorser_guide.png\",\"BodyGuideUrl\":\"/images/tolak_endorser_guide.png\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "tolak-angin-live",
                column: "SlotConfigJson",
                value: null);
        }
    }
}
