using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedEventSlidesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameInstanceEvents_GameInstanceShows_GameInstanceShowId",
                table: "GameInstanceEvents");

            migrationBuilder.DropTable(
                name: "GameInstanceShows");

            migrationBuilder.DropIndex(
                name: "IX_GameInstanceEvents_GameInstanceShowId",
                table: "GameInstanceEvents");

            migrationBuilder.DropIndex(
                name: "IX_GameInstanceEventResponses_GameInstanceEventId_ParticipantId",
                table: "GameInstanceEventResponses");

            migrationBuilder.DropColumn(
                name: "GameInstanceShowId",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "SlideType",
                table: "GameInstanceEvents");

            migrationBuilder.RenameColumn(
                name: "SourceSlideId",
                table: "GameInstanceEvents",
                newName: "SourceShowId");

            migrationBuilder.RenameColumn(
                name: "ParametersJson",
                table: "GameInstanceEvents",
                newName: "SlidesJson");

            migrationBuilder.AddColumn<int>(
                name: "CurrentSlideIndex",
                table: "GameInstanceEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NavigationMode",
                table: "GameInstanceEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "host_controlled");

            migrationBuilder.AddColumn<Guid>(
                name: "SlideId",
                table: "GameInstanceEventResponses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEventResponses_GameInstanceEventId_SlideId_Part~",
                table: "GameInstanceEventResponses",
                columns: new[] { "GameInstanceEventId", "SlideId", "ParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameInstanceEventResponses_GameInstanceEventId_SlideId_Part~",
                table: "GameInstanceEventResponses");

            migrationBuilder.DropColumn(
                name: "CurrentSlideIndex",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "NavigationMode",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "SlideId",
                table: "GameInstanceEventResponses");

            migrationBuilder.RenameColumn(
                name: "SourceShowId",
                table: "GameInstanceEvents",
                newName: "SourceSlideId");

            migrationBuilder.RenameColumn(
                name: "SlidesJson",
                table: "GameInstanceEvents",
                newName: "ParametersJson");

            migrationBuilder.AddColumn<Guid>(
                name: "GameInstanceShowId",
                table: "GameInstanceEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlideType",
                table: "GameInstanceEvents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GameInstanceShows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentSlideIndex = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstanceShows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstanceShows_GameInstances_GameInstanceId",
                        column: x => x.GameInstanceId,
                        principalTable: "GameInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameInstanceShows_Shows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "Shows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEvents_GameInstanceShowId",
                table: "GameInstanceEvents",
                column: "GameInstanceShowId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEventResponses_GameInstanceEventId_ParticipantId",
                table: "GameInstanceEventResponses",
                columns: new[] { "GameInstanceEventId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceShows_GameInstanceId",
                table: "GameInstanceShows",
                column: "GameInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceShows_ShowId",
                table: "GameInstanceShows",
                column: "ShowId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameInstanceEvents_GameInstanceShows_GameInstanceShowId",
                table: "GameInstanceEvents",
                column: "GameInstanceShowId",
                principalTable: "GameInstanceShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
