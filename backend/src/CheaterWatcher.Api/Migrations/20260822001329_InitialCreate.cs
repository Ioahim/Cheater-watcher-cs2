using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CheaterWatcher.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PremierRating = table.Column<int>(type: "integer", nullable: true),
                    WingmanLevel = table.Column<int>(type: "integer", nullable: true),
                    Steam64Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AuthCode = table.Column<string>(type: "text", nullable: true),
                    LatestShareCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "player_stats_cache",
                columns: table => new
                {
                    Steam64Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_stats_cache", x => x.Steam64Id);
                });

            migrationBuilder.CreateTable(
                name: "account_map_ranks",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Map = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_map_ranks", x => new { x.AccountId, x.Map });
                    table.ForeignKey(
                        name: "FK_account_map_ranks_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    MapName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CtScore = table.Column<int>(type: "integer", nullable: false),
                    TScore = table.Column<int>(type: "integer", nullable: false),
                    OurTeamNumber = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    DemoFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DemoSourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Suspected = table.Column<bool>(type: "boolean", nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matches_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Steam64Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeamNumber = table.Column<int>(type: "integer", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    SuspicionScore = table.Column<double>(type: "double precision", nullable: true),
                    SuspicionBreakdownJson = table.Column<string>(type: "jsonb", nullable: true),
                    RankType = table.Column<short>(type: "smallint", nullable: true),
                    RankValue = table.Column<int>(type: "integer", nullable: true),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_match_players_matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Steam64Id",
                table: "accounts",
                column: "Steam64Id",
                unique: true,
                filter: "\"Steam64Id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_match_players_MatchId",
                table: "match_players",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_match_players_Steam64Id",
                table: "match_players",
                column: "Steam64Id");

            migrationBuilder.CreateIndex(
                name: "IX_matches_AccountId_DemoSourceId",
                table: "matches",
                columns: new[] { "AccountId", "DemoSourceId" },
                unique: true,
                filter: "\"DemoSourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_matches_FinishedAt",
                table: "matches",
                column: "FinishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_map_ranks");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "player_stats_cache");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
