using System.Globalization;
using System.Text.Json;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Infrastructure;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Ingestion;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(
    AppDbContext db,
    ShareCodeIngestionService shareIngestion,
    SteamWebApiClient steam) : ControllerBase
{
    // Guests see only ownerless demo accounts; authenticated users see their own.
    private IQueryable<Account> VisibleAccounts()
    {
        var userId = User.TryGetUserId();
        return db.Accounts.Where(a => userId == null ? a.UserId == null : a.UserId == userId);
    }

    [HttpGet]
    [EnableRateLimiting("external")]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAccounts(CancellationToken ct)
    {
        var accounts = await VisibleAccounts()
            .Include(a => a.MapRanks)
            .Include(a => a.User)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        // Best-effort enrichment: refresh the Steam persona name + avatar for linked
        // accounts that don't have one yet (idempotent; skipped without an API key).
        var enriched = false;
        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Steam64Id))
                continue;
            if (!string.IsNullOrWhiteSpace(account.User?.AvatarUrl))
                continue;

            var summary = await steam.GetPlayerSummariesAsync(account.Steam64Id, ct);
            if (summary is null)
                continue;

            if (!string.IsNullOrWhiteSpace(summary.AvatarFull) && account.User is { } user)
                user.AvatarUrl = summary.AvatarFull;
            if (!string.IsNullOrWhiteSpace(summary.PersonaName))
                account.Name = summary.PersonaName;
            enriched = true;
        }
        if (enriched)
            await db.SaveChangesAsync(ct);

        return Ok(accounts.Select(AccountDto.From));
    }

    [HttpGet("{id:int}/matches")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetMatches(int id, CancellationToken ct)
    {
        var account = await VisibleAccounts().AsNoTracking()
            .Include(a => a.MapRanks)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return NotFound();

        var matches = await db.Matches.AsNoTracking()
            .Where(m => m.AccountId == id)
            .OrderByDescending(m => m.FinishedAt)
            .ToListAsync(ct);

        return Ok(matches.Select(m => MatchMapper.ToDto(m, account)));
    }

    [Authorize]
    [HttpPatch("{id:int}/credentials")]
    public async Task<IActionResult> UpdateCredentials(int id, UpdateCredentialsRequest request, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null || account.UserId != userId)
            return NotFound(new { error = "Account not found." });

        var oldSteam64 = account.Steam64Id;
        var newSteam64 = string.IsNullOrWhiteSpace(request.Steam64Id) ? null : request.Steam64Id.Trim();
        account.Steam64Id = newSteam64;
        account.AuthCode = string.IsNullOrWhiteSpace(request.AuthCode) ? null : request.AuthCode.Trim();

        // A share-code cursor is tied to the Steam account it was generated for. If the
        // account is re-linked to a different Steam64 (or unlinked), drop the stale cursor
        // so polling never uses the old account's code against the new one.
        if (string.IsNullOrWhiteSpace(request.ShareCode))
        {
            if (newSteam64 != oldSteam64)
                account.LatestShareCode = null;
        }

        await db.SaveChangesAsync(ct);

        if (string.IsNullOrWhiteSpace(request.ShareCode))
            return Ok(null);

        var result = await shareIngestion.IngestAsync(db, account.Id, request.ShareCode.Trim(), ct);

        // Only advance the polling cursor once the code actually decodes/ingests (or is a
        // known duplicate). Persisting it up front could leave polling pointed at an
        // undecodable or failing share code.
        if (result.Status is "ingested" or "duplicate")
        {
            account.LatestShareCode = request.ShareCode.Trim();
            await db.SaveChangesAsync(ct);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Unlink(int id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var account = await db.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null || account.UserId != userId)
            return NotFound(new { error = "Account not found." });

        var steam64 = account.Steam64Id;
        account.Steam64Id = null;
        account.AuthCode = null;
        account.LatestShareCode = null;

        // Detach the user's own Steam link so the account can be re-linked freely.
        if (account.User is { } user && user.Steam64Id == steam64)
            user.Steam64Id = null;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/stats")]
    public async Task<ActionResult<AccountStatsDto>> GetStats(int id, CancellationToken ct)
    {
        var account = await VisibleAccounts().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return NotFound();

        var matches = await db.Matches.AsNoTracking()
            .Where(m => m.AccountId == id && m.Status == ParseStatus.Parsed)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();

        var players = await db.MatchPlayers.AsNoTracking()
            .Include(p => p.Match)
            .Where(p => matchIds.Contains(p.MatchId))
            .ToListAsync(ct);

        var totalMatches = matches.Count;
        var flaggedMatches = matches.Count(m => m.FlaggedAt is not null);

        int wins = 0;
        foreach (var m in matches)
        {
            var (our, their) = MatchScore.SplitScores(m.CtScore, m.TScore, m.OurTeamNumber);
            if (our > their) wins++;
        }

        var flaggedPlayers = players.Count(p => p.FlaggedAt is not null);
        var distinctPlayers = players.Select(p => p.Steam64Id).Distinct().Count();

        var byMap = players.GroupBy(p => MapNames.Display(p.Match.MapName))
            .Select(g =>
            {
                var mapMatches = matches.Where(m => MapNames.Display(m.MapName) == g.Key).ToList();
                int mapWins = 0;
                foreach (var m in mapMatches)
                {
                    var (our, their) = MatchScore.SplitScores(m.CtScore, m.TScore, m.OurTeamNumber);
                    if (our > their) mapWins++;
                }
                return new MapStatDto(g.Key, mapMatches.Count, mapMatches.Count > 0 ? (double)mapWins / mapMatches.Count : 0);
            })
            .OrderByDescending(x => x.Matches)
            .ToList();

        var byMode = matches.GroupBy(m => m.Mode)
            .Select(g => new ModeStatDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Matches)
            .ToList();

        return Ok(new AccountStatsDto(
            totalMatches,
            flaggedMatches,
            flaggedPlayers,
            totalMatches > 0 ? (double)wins / totalMatches : 0,
            distinctPlayers,
            byMap,
            byMode));
    }
}

public static class MatchMapper
{
    public static MatchDto ToDto(Match match, Account account)
    {
        var (ourScore, theirScore) = MatchScore.SplitScores(match.CtScore, match.TScore, match.OurTeamNumber);
        var result = MatchScore.ResultChar(match.OurTeamNumber, ourScore, theirScore);

        return new MatchDto(
            match.Id.ToString(),
            result,
            $"{ourScore}-{theirScore}",
            MapNames.Display(match.MapName),
            match.Mode,
            ResolveRank(account, match),
            match.FinishedAt.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
            match.Suspected,
            match.FlaggedAt is not null,
            match.Status.ToString());
    }

    private static RankDto? ResolveRank(Account account, Match match) => match.Mode switch
    {
        "Premier" => account.PremierRating is { } rating
            ? new RankDto("premier", rating, null)
            : null,
        "Wingman" => account.WingmanLevel is { } wingman
            ? new RankDto("wingman", null, wingman)
            : null,
        "Competitive" => MapNames.Display(match.MapName) is { } map
                         && account.MapRanks.FirstOrDefault(r => r.Map == map) is { } row
            ? new RankDto("competitive", null, row.Level)
            : null,
        _ => null,
    };
}
