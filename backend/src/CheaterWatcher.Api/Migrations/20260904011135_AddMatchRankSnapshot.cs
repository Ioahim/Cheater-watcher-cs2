using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheaterWatcher.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRankSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "OwnRankType",
                table: "matches",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnRankValue",
                table: "matches",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_matches_AccountId_Mode_FinishedAt",
                table: "matches",
                columns: new[] { "AccountId", "Mode", "FinishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_AccountId_Mode_FinishedAt",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "OwnRankType",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "OwnRankValue",
                table: "matches");
        }
    }
}
