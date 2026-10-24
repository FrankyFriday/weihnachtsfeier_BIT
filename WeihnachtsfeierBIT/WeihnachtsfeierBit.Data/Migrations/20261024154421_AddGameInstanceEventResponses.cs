using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstanceEventResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameInstanceEventResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstanceEventResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstanceEventResponses_GameInstanceEvents_GameInstanceE~",
                        column: x => x.GameInstanceEventId,
                        principalTable: "GameInstanceEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameInstanceEventResponses_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEventResponses_GameInstanceEventId_ParticipantId",
                table: "GameInstanceEventResponses",
                columns: new[] { "GameInstanceEventId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEventResponses_ParticipantId",
                table: "GameInstanceEventResponses",
                column: "ParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameInstanceEventResponses");
        }
    }
}
