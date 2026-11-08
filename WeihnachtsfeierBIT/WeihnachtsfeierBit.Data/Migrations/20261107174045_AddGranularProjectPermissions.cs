using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGranularProjectPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanControlSession",
                table: "ProjektAdminProjekte",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanEditProjectSettings",
                table: "ProjektAdminProjekte",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageParticipants",
                table: "ProjektAdminProjekte",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageShows",
                table: "ProjektAdminProjekte",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanControlSession",
                table: "ProjektAdminProjekte");

            migrationBuilder.DropColumn(
                name: "CanEditProjectSettings",
                table: "ProjektAdminProjekte");

            migrationBuilder.DropColumn(
                name: "CanManageParticipants",
                table: "ProjektAdminProjekte");

            migrationBuilder.DropColumn(
                name: "CanManageShows",
                table: "ProjektAdminProjekte");
        }
    }
}
