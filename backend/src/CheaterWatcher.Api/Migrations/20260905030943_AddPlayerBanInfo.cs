using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CheaterWatcher.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerBanInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_ban_info",
                columns: table => new
                {
                    Steam64Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CommunityBanned = table.Column<bool>(type: "boolean", nullable: false),
                    VacBanned = table.Column<bool>(type: "boolean", nullable: false),
                    NumberOfVACBans = table.Column<int>(type: "integer", nullable: false),
                    NumberOfGameBans = table.Column<int>(type: "integer", nullable: false),
                    DaysSinceLastBan = table.Column<int>(type: "integer", nullable: false),
                    EconomyBan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_ban_info", x => x.Steam64Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_ban_info");
        }
    }
}
