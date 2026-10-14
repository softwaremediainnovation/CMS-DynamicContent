using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotConfigJsonToDynamicContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SlotConfigJson",
                table: "DynamicContentMasters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "samsung-zfold5",
                column: "SlotConfigJson",
                value: null);

            migrationBuilder.UpdateData(
                table: "DynamicContentMasters",
                keyColumn: "Id",
                keyValue: "tolak-angin-live",
                column: "SlotConfigJson",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlotConfigJson",
                table: "DynamicContentMasters");
        }
    }
}
