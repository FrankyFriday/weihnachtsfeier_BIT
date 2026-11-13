using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeilnehmerBerechtigungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeilnehmerBerechtigungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeilnehmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeilnehmerBerechtigungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeilnehmerBerechtigungen_Teilnehmer_TeilnehmerId",
                        column: x => x.TeilnehmerId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeilnehmerBerechtigungen_TeilnehmerId",
                table: "TeilnehmerBerechtigungen",
                column: "TeilnehmerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeilnehmerBerechtigungen");
        }
    }
}
