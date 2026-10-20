using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameInstanceId",
                table: "Sitzungen",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstances_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Punkte = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamInstances_GameInstances_GameInstanceId",
                        column: x => x.GameInstanceId,
                        principalTable: "GameInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamInstances_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeilnehmerInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeilnehmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeilnehmerInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeilnehmerInstances_GameInstances_GameInstanceId",
                        column: x => x.GameInstanceId,
                        principalTable: "GameInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeilnehmerInstances_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeilnehmerInstances_Teilnehmer_TeilnehmerId",
                        column: x => x.TeilnehmerId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sitzungen_GameInstanceId",
                table: "Sitzungen",
                column: "GameInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstances_AccessCode",
                table: "GameInstances",
                column: "AccessCode");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstances_ProjektId_IsActive",
                table: "GameInstances",
                columns: new[] { "ProjektId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamInstances_GameInstanceId_TeamId",
                table: "TeamInstances",
                columns: new[] { "GameInstanceId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamInstances_TeamId",
                table: "TeamInstances",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeilnehmerInstances_GameInstanceId_TeilnehmerId",
                table: "TeilnehmerInstances",
                columns: new[] { "GameInstanceId", "TeilnehmerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeilnehmerInstances_TeamId",
                table: "TeilnehmerInstances",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeilnehmerInstances_TeilnehmerId",
                table: "TeilnehmerInstances",
                column: "TeilnehmerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sitzungen_GameInstances_GameInstanceId",
                table: "Sitzungen",
                column: "GameInstanceId",
                principalTable: "GameInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sitzungen_GameInstances_GameInstanceId",
                table: "Sitzungen");

            migrationBuilder.DropTable(
                name: "TeamInstances");

            migrationBuilder.DropTable(
                name: "TeilnehmerInstances");

            migrationBuilder.DropTable(
                name: "GameInstances");

            migrationBuilder.DropIndex(
                name: "IX_Sitzungen_GameInstanceId",
                table: "Sitzungen");

            migrationBuilder.DropColumn(
                name: "GameInstanceId",
                table: "Sitzungen");
        }
    }
}
