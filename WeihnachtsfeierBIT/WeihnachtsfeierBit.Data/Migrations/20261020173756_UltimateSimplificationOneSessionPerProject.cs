using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class UltimateSimplificationOneSessionPerProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamScores");

            migrationBuilder.DropIndex(
                name: "IX_GameInstances_ProjektId_IsActive",
                table: "GameInstances");

            migrationBuilder.DropColumn(
                name: "EndedAtUtc",
                table: "GameInstances");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GameInstances");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "GameInstances");

            migrationBuilder.AddColumn<int>(
                name: "Punkte",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "AccessCode",
                table: "GameInstances",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6,
                oldNullable: true);

            migrationBuilder.Sql(@"
                DELETE FROM ""GameInstances""
                WHERE ""Id"" NOT IN (
                    SELECT DISTINCT ON (""ProjektId"") ""Id""
                    FROM ""GameInstances""
                    ORDER BY ""ProjektId"", ""CreatedAtUtc"" DESC
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_GameInstances_ProjektId",
                table: "GameInstances",
                column: "ProjektId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameInstances_ProjektId",
                table: "GameInstances");

            migrationBuilder.DropColumn(
                name: "Punkte",
                table: "Teams");

            migrationBuilder.AlterColumn<string>(
                name: "AccessCode",
                table: "GameInstances",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAtUtc",
                table: "GameInstances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GameInstances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "GameInstances",
                type: "timestamp with time zone",
                nullable: true);

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
                name: "IX_GameInstances_ProjektId_IsActive",
                table: "GameInstances",
                columns: new[] { "ProjektId", "IsActive" });

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
    }
}
