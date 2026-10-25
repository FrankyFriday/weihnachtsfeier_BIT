using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeihnachtsfeierBit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatUserReadsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatUserReads_ChatGroupId",
                table: "ChatUserReads");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ChatGroupId_ParticipantId",
                table: "ChatUserReads",
                columns: new[] { "ChatGroupId", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ChatGroupId_ProjektAdminId",
                table: "ChatUserReads",
                columns: new[] { "ChatGroupId", "ProjektAdminId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatUserReads_ChatGroupId_ParticipantId",
                table: "ChatUserReads");

            migrationBuilder.DropIndex(
                name: "IX_ChatUserReads_ChatGroupId_ProjektAdminId",
                table: "ChatUserReads");

            migrationBuilder.CreateIndex(
                name: "IX_ChatUserReads_ChatGroupId",
                table: "ChatUserReads",
                column: "ChatGroupId");
        }
    }
}
