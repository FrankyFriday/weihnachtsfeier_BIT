using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSchedulingAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoStartOnSchedule",
                table: "GameInstanceEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "GameInstanceEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartUtc",
                table: "GameInstanceEvents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoStartOnSchedule",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "ScheduledStartUtc",
                table: "GameInstanceEvents");
        }
    }
}
