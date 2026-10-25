using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatUserReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatUserReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjektAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatUserReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatUserReads_ChatGroups_ChatGroupId",
                        column: x => x.ChatGroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatUserReads_ProjektAdmins_ProjektAdminId",
                        column: x => x.ProjektAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatUserReads_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ChatGroupId",
                table: "ChatUserReads",
                column: "ChatGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ParticipantId",
                table: "ChatUserReads",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ProjektAdminId",
                table: "ChatUserReads",
                column: "ProjektAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatUserReads");
        }
    }
}
