using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInfoPagesAndPartyFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectInfoPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInfoPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInfoPages_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SongWishes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjektId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTitle = table.Column<string>(type: "text", nullable: false),
                    Artist = table.Column<string>(type: "text", nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongWishes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SongWishes_Projekte_ProjektId",
                        column: x => x.ProjektId,
                        principalTable: "Projekte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SongWishes_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamNameSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedName = table.Column<string>(type: "text", nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamNameSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamNameSuggestions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamNameSuggestions_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SongWishVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SongWishId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongWishVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SongWishVotes_SongWishes_SongWishId",
                        column: x => x.SongWishId,
                        principalTable: "SongWishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SongWishVotes_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamNameVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamNameVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamNameVotes_TeamNameSuggestions_SuggestionId",
                        column: x => x.SuggestionId,
                        principalTable: "TeamNameSuggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamNameVotes_Teilnehmer_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Teilnehmer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInfoPages_ProjektId",
                table: "ProjectInfoPages",
                column: "ProjektId");

            migrationBuilder.CreateIndex(
                name: "IX_SongWishes_ParticipantId",
                table: "SongWishes",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_SongWishes_ProjektId",
                table: "SongWishes",
                column: "ProjektId");

            migrationBuilder.CreateIndex(
                name: "IX_SongWishVotes_ParticipantId",
                table: "SongWishVotes",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_SongWishVotes_SongWishId_ParticipantId",
                table: "SongWishVotes",
                columns: new[] { "SongWishId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamNameSuggestions_ParticipantId",
                table: "TeamNameSuggestions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamNameSuggestions_TeamId",
                table: "TeamNameSuggestions",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamNameVotes_ParticipantId",
                table: "TeamNameVotes",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamNameVotes_SuggestionId_ParticipantId",
                table: "TeamNameVotes",
                columns: new[] { "SuggestionId", "ParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectInfoPages");

            migrationBuilder.DropTable(
                name: "SongWishVotes");

            migrationBuilder.DropTable(
                name: "TeamNameVotes");

            migrationBuilder.DropTable(
                name: "SongWishes");

            migrationBuilder.DropTable(
                name: "TeamNameSuggestions");
        }
    }
}
