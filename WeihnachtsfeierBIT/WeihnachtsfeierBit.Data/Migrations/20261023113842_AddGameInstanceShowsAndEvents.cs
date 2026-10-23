using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstanceShowsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameInstanceShows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    CurrentSlideIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "GameInstanceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameInstanceShowId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSlideId = table.Column<Guid>(type: "uuid", nullable: true),
                    SlideType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "active"),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstanceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstanceEvents_GameInstanceShows_GameInstanceShowId",
                        column: x => x.GameInstanceShowId,
                        principalTable: "GameInstanceShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameInstanceEvents_GameInstances_GameInstanceId",
                        column: x => x.GameInstanceId,
                        principalTable: "GameInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEvents_GameInstanceId",
                table: "GameInstanceEvents",
                column: "GameInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEvents_GameInstanceShowId",
                table: "GameInstanceEvents",
                column: "GameInstanceShowId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceShows_GameInstanceId",
                table: "GameInstanceShows",
                column: "GameInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceShows_ShowId",
                table: "GameInstanceShows",
                column: "ShowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameInstanceEvents");

            migrationBuilder.DropTable(
                name: "GameInstanceShows");
        }
    }
}
