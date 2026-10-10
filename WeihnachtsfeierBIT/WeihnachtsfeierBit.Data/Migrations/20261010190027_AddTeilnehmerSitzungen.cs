using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeilnehmerSitzungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sitzungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeilnehmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErstelltAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LetzteAktivitaetAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LaeuftAbAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WiderrufenAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sitzungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sitzungen_Teilnehmer_TeilnehmerId",
                        column: x => x.TeilnehmerId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sitzungen_TeilnehmerId_WiderrufenAmUtc_LaeuftAbAmUtc",
                table: "Sitzungen",
                columns: new[] { "TeilnehmerId", "WiderrufenAmUtc", "LaeuftAbAmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sitzungen_TokenHash",
                table: "Sitzungen",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sitzungen");
        }
    }
}
