using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class GlobalAdminAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjektAdminProjekte",
                columns: table => new
                {
                    ProjektAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    HinzugefuegtAmUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjektAdminProjekte", x => new { x.ProjektAdminId, x.ProjektId });
                    table.ForeignKey(
                        name: "FK_ProjektAdminProjekte_ProjektAdmins_ProjektAdminId",
                        column: x => x.ProjektAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjektAdminProjekte_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ProjektAdminProjekte" ("ProjektAdminId", "ProjektId", "HinzugefuegtAmUtc")
                SELECT "Id", "ProjektId", "ErstelltAmUtc"
                FROM "ProjektAdmins";
                """);

            migrationBuilder.Sql(
                """
                WITH duplicate_accounts AS (
                    SELECT "Id", COUNT(*) OVER (PARTITION BY "BenutzernameNormalisiert") AS account_count
                    FROM "ProjektAdmins"
                )
                UPDATE "ProjektAdmins" AS account
                SET "Benutzername" = LEFT(account."Benutzername", 54) || '-' ||
                        LEFT(REPLACE(account."Id"::text, '-', ''), 8),
                    "BenutzernameNormalisiert" = UPPER(LEFT(account."Benutzername", 54)) || '-' ||
                        UPPER(LEFT(REPLACE(account."Id"::text, '-', ''), 8))
                FROM duplicate_accounts
                WHERE duplicate_accounts."Id" = account."Id"
                    AND duplicate_accounts.account_count > 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ProjektAdmins_Projekte_ProjektId",
                table: "ProjektAdmins");

            migrationBuilder.DropIndex(
                name: "IX_ProjektAdmins_ProjektId_BenutzernameNormalisiert",
                table: "ProjektAdmins");

            migrationBuilder.DropColumn(
                name: "ProjektId",
                table: "ProjektAdmins");

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdmins_BenutzernameNormalisiert",
                table: "ProjektAdmins",
                column: "BenutzernameNormalisiert",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdminProjekte_ProjektId",
                table: "ProjektAdminProjekte",
                column: "ProjektId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "ProjektAdmins" AS admin
                        LEFT JOIN "ProjektAdminProjekte" AS membership
                            ON membership."ProjektAdminId" = admin."Id"
                        GROUP BY admin."Id"
                        HAVING COUNT(membership."ProjektId") <> 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade global admin accounts while an account has zero or multiple project memberships.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ProjektAdmins_BenutzernameNormalisiert",
                table: "ProjektAdmins");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjektId",
                table: "ProjektAdmins",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ProjektAdmins" AS admin
                SET "ProjektId" = membership."ProjektId"
                FROM "ProjektAdminProjekte" AS membership
                WHERE membership."ProjektAdminId" = admin."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjektId",
                table: "ProjektAdmins",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "ProjektAdminProjekte");

            migrationBuilder.CreateIndex(
                name: "IX_ProjektAdmins_ProjektId_BenutzernameNormalisiert",
                table: "ProjektAdmins",
                columns: new[] { "ProjektId", "BenutzernameNormalisiert" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjektAdmins_Projekte_ProjektId",
                table: "ProjektAdmins",
                column: "ProjektId",
                principalTable: "Projekte",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
