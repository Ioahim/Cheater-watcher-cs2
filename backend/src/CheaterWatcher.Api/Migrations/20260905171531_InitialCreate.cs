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
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

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
                name: "processed_replays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    LastWriteTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_replays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "replay_scan_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HostPath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    LastScanAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScanNew = table.Column<int>(type: "integer", nullable: false),
                    LastScanAttributed = table.Column<int>(type: "integer", nullable: false),
                    LastScanPending = table.Column<int>(type: "integer", nullable: false),
                    LastScanError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replay_scan_settings", x => x.Id);
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
                    OwnRankType = table.Column<short>(type: "smallint", nullable: true),
                    OwnRankValue = table.Column<int>(type: "integer", nullable: true),
                    Suspected = table.Column<bool>(type: "boolean", nullable: false),
                    ScoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeleteDemoAfterParse = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "pending_replays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    LastWriteTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MapName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAccountId = table.Column<int>(type: "integer", nullable: true),
                    PlayerSteamIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PlayerNamesJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_replays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_replays_accounts_ResolvedAccountId",
                        column: x => x.ResolvedAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FlagReason = table.Column<int>(type: "integer", nullable: false),
                    FlagNote = table.Column<string>(type: "text", nullable: true)
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
                name: "IX_match_players_MatchId_Steam64Id",
                table: "match_players",
                columns: new[] { "MatchId", "Steam64Id" });

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
                name: "IX_matches_AccountId_Mode_FinishedAt",
                table: "matches",
                columns: new[] { "AccountId", "Mode", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_AccountId_Status",
                table: "matches",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_FinishedAt",
                table: "matches",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_pending_replays_FileHash",
                table: "pending_replays",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_pending_replays_ResolvedAccountId",
                table: "pending_replays",
                column: "ResolvedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_pending_replays_Status",
                table: "pending_replays",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_processed_replays_FileHash",
                table: "processed_replays",
                column: "FileHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_map_ranks");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "pending_replays");

            migrationBuilder.DropTable(
                name: "player_ban_info");

            migrationBuilder.DropTable(
                name: "player_stats_cache");

            migrationBuilder.DropTable(
                name: "processed_replays");

            migrationBuilder.DropTable(
                name: "replay_scan_settings");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
