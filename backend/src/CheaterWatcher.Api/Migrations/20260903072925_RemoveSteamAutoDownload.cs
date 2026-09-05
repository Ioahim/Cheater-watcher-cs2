using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CheaterWatcher.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSteamAutoDownload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_queue");

            migrationBuilder.DropColumn(
                name: "AuthCode",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "LatestShareCode",
                table: "accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthCode",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestShareCode",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "match_queue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    MatchRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DemoFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MatchId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OutcomeId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_queue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_match_queue_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_match_queue_matches_MatchRecordId",
                        column: x => x.MatchRecordId,
                        principalTable: "matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_queue_AccountId",
                table: "match_queue",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_match_queue_CreatedAt",
                table: "match_queue",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_match_queue_MatchId_AccountId",
                table: "match_queue",
                columns: new[] { "MatchId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_queue_MatchRecordId",
                table: "match_queue",
                column: "MatchRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_match_queue_Status",
                table: "match_queue",
                column: "Status");
        }
    }
}
