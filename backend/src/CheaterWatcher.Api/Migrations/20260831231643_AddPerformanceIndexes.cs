using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheaterWatcher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_match_players_MatchId",
                table: "match_players");

            migrationBuilder.CreateIndex(
                name: "IX_matches_AccountId_Status",
                table: "matches",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_match_players_MatchId_Steam64Id",
                table: "match_players",
                columns: new[] { "MatchId", "Steam64Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_AccountId_Status",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "IX_match_players_MatchId_Steam64Id",
                table: "match_players");

            migrationBuilder.CreateIndex(
                name: "IX_match_players_MatchId",
                table: "match_players",
                column: "MatchId");
        }
    }
}
