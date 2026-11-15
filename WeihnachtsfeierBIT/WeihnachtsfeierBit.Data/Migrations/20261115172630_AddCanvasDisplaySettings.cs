using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasDisplaySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanvasSettingsJson",
                table: "GameInstances",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"scoreboardLayout\":\"right\",\"qrCodeMode\":\"card\"}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanvasSettingsJson",
                table: "GameInstances");
        }
    }
}
