using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInstancesAndAddTeamScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamInstances");

            migrationBuilder.DropTable(
                name: "TeilnehmerInstances");

            migrationBuilder.CreateTable(
                name: "TeamScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamScores_GameInstances_GameInstanceId",
                        column: x => x.GameInstanceId,
                        principalTable: "GameInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamScores_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamScores_GameInstanceId_TeamId",
                table: "TeamScores",
                columns: new[] { "GameInstanceId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamScores_TeamId",
                table: "TeamScores",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamScores");

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
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeilnehmerId = table.Column<Guid>(type: "uuid", nullable: false)
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
        }
    }
}
