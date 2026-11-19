using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameInstanceEventPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameInstanceEvents_GameInstanceId",
                table: "GameInstanceEvents");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "GameInstanceEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing events keep their creation order as initial manual order.
            migrationBuilder.Sql("""
                UPDATE "GameInstanceEvents" AS e
                SET "Position" = ordered.rn - 1
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "GameInstanceId" ORDER BY "StartedAtUtc", "Id") AS rn
                    FROM "GameInstanceEvents"
                ) AS ordered
                WHERE e."Id" = ordered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEvents_GameInstanceId_Position",
                table: "GameInstanceEvents",
                columns: new[] { "GameInstanceId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameInstanceEvents_GameInstanceId_Position",
                table: "GameInstanceEvents");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "GameInstanceEvents");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstanceEvents_GameInstanceId",
                table: "GameInstanceEvents",
                column: "GameInstanceId");
        }
    }
}
