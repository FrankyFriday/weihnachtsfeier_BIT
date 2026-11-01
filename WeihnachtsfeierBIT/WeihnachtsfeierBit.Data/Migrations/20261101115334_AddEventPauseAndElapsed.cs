using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPauseAndElapsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccumulatedElapsedSeconds",
                table: "GameInstanceEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "GameInstanceEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResumedAtUtc",
                table: "GameInstanceEvents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedElapsedSeconds",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "LastResumedAtUtc",
                table: "GameInstanceEvents");
        }
    }
}
