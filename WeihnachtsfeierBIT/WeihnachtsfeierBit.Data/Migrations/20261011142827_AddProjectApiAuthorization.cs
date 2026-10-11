using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectApiAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teilnehmer_ProjektId",
                table: "Teilnehmer");

            migrationBuilder.CreateTable(
                name: "ProjektAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    Benutzername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BenutzernameNormalisiert = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswortHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ErstelltAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjektAdmins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjektAdmins_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjektAdminSitzungen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErstelltAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LetzteAktivitaetAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LaeuftAbAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WiderrufenAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjektAdminSitzungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjektAdminSitzungen_ProjektAdmins_ProjektAdminId",
                        column: x => x.ProjektAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teilnehmer_ProjektId_Nummer",
                table: "Teilnehmer",
                columns: new[] { "ProjektId", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdmins_ProjektId_BenutzernameNormalisiert",
                table: "ProjektAdmins",
                columns: new[] { "ProjektId", "BenutzernameNormalisiert" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdminSitzungen_ProjektAdminId_WiderrufenAmUtc_Laeuft~",
                table: "ProjektAdminSitzungen",
                columns: new[] { "ProjektAdminId", "WiderrufenAmUtc", "LaeuftAbAmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdminSitzungen_TokenHash",
                table: "ProjektAdminSitzungen",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjektAdminSitzungen");

            migrationBuilder.DropTable(
                name: "ProjektAdmins");

            migrationBuilder.DropIndex(
                name: "IX_Teilnehmer_ProjektId_Nummer",
                table: "Teilnehmer");

            migrationBuilder.CreateIndex(
                name: "IX_Teilnehmer_ProjektId",
                table: "Teilnehmer",
                column: "ProjektId");
        }
    }
}
