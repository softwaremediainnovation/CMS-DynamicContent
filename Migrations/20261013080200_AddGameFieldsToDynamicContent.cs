using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CmsBillboard.DynamicContent.Migrations
{
    /// <inheritdoc />
    public partial class AddGameFieldsToDynamicContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameFolder",
                table: "DynamicContentMasters",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameEntryFile",
                table: "DynamicContentMasters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameCustomParams",
                table: "DynamicContentMasters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameFolder",
                table: "DynamicContentMasters");

            migrationBuilder.DropColumn(
                name: "GameEntryFile",
                table: "DynamicContentMasters");

            migrationBuilder.DropColumn(
                name: "GameCustomParams",
                table: "DynamicContentMasters");
        }
    }
}
