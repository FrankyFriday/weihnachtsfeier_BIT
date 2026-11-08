using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAndShowSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ErstellerAdminId",
                table: "Shows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ErstellerAdminId",
                table: "Projekte",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjektAdminShows",
                columns: table => new
                {
                    ProjektAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    HinzugefuegtAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjektAdminShows", x => new { x.ProjektAdminId, x.ShowId });
                    table.ForeignKey(
                        name: "FK_ProjektAdminShows_ProjektAdmins_ProjektAdminId",
                        column: x => x.ProjektAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjektAdminShows_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shows_ErstellerAdminId",
                table: "Shows",
                column: "ErstellerAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Projekte_ErstellerAdminId",
                table: "Projekte",
                column: "ErstellerAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdminShows_ShowId",
                table: "ProjektAdminShows",
                column: "ShowId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projekte_ProjektAdmins_ErstellerAdminId",
                table: "Projekte",
                column: "ErstellerAdminId",
                principalTable: "ProjektAdmins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Shows_ProjektAdmins_ErstellerAdminId",
                table: "Shows",
                column: "ErstellerAdminId",
                principalTable: "ProjektAdmins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projekte_ProjektAdmins_ErstellerAdminId",
                table: "Projekte");

            migrationBuilder.DropForeignKey(
                name: "FK_Shows_ProjektAdmins_ErstellerAdminId",
                table: "Shows");

            migrationBuilder.DropTable(
                name: "ProjektAdminShows");

            migrationBuilder.DropIndex(
                name: "IX_Shows_ErstellerAdminId",
                table: "Shows");

            migrationBuilder.DropIndex(
                name: "IX_Projekte_ErstellerAdminId",
                table: "Projekte");

            migrationBuilder.DropColumn(
                name: "ErstellerAdminId",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "ErstellerAdminId",
                table: "Projekte");
        }
    }
}
