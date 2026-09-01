using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheaterWatcher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerFlagCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagNote",
                table: "match_players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FlagReason",
                table: "match_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagNote",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "FlagReason",
                table: "match_players");
        }
    }
}
