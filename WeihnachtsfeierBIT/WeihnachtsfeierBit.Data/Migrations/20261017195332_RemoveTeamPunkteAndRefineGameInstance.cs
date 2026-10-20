using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeamPunkteAndRefineGameInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Punkte",
                table: "Teams");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "GameInstances",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "GameInstances");

            migrationBuilder.AddColumn<int>(
                name: "Punkte",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
