using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReadStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAtUtc",
                table: "ChatGroupMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminChatReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminChatReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminChatReads_ChatGroups_ChatGroupId",
                        column: x => x.ChatGroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminChatReads_ProjektAdmins_ProjektAdminId",
                        column: x => x.ProjektAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminChatReads_ChatGroupId_ProjektAdminId",
                table: "AdminChatReads",
                columns: new[] { "ChatGroupId", "ProjektAdminId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminChatReads_ProjektAdminId",
                table: "AdminChatReads",
                column: "ProjektAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminChatReads");

            migrationBuilder.DropColumn(
                name: "LastReadAtUtc",
                table: "ChatGroupMembers");
        }
    }
}
