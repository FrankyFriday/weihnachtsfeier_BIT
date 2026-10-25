using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "global"),
                    AssociatedTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatGroups_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatGroups_Teams_AssociatedTeamId",
                        column: x => x.AssociatedTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChatGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatGroupMembers_ChatGroups_ChatGroupId",
                        column: x => x.ChatGroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatGroupMembers_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    TeamColor = table.Column<string>(type: "text", nullable: true),
                    MessageText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatGroups_ChatGroupId",
                        column: x => x.ChatGroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ProjektAdmins_SenderAdminId",
                        column: x => x.SenderAdminId,
                        principalTable: "ProjektAdmins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Teilnehmer_SenderParticipantId",
                        column: x => x.SenderParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroupMembers_ChatGroupId_ParticipantId",
                table: "ChatGroupMembers",
                columns: new[] { "ChatGroupId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroupMembers_ParticipantId",
                table: "ChatGroupMembers",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_AssociatedTeamId",
                table: "ChatGroups",
                column: "AssociatedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_ProjektId",
                table: "ChatGroups",
                column: "ProjektId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatGroupId",
                table: "ChatMessages",
                column: "ChatGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderAdminId",
                table: "ChatMessages",
                column: "SenderAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderParticipantId",
                table: "ChatMessages",
                column: "SenderParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatGroupMembers");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ChatGroups");
        }
    }
}
