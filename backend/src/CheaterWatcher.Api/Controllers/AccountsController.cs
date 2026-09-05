using System.Globalization;
using System.Text.Json;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Auth;
using CheaterWatcher.Api.Services.Ingestion;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(
    AppDbContext db,
    SteamWebApiClient steam,
    SteamOpenIdService openId,
    IMemoryCache cache,
    PendingReplayResolver pendingResolver,
    IOptions<OpenIdOptions> openIdOptions) : ControllerBase
{
    private const string LinkStatePrefix = "steam:link:";
    private const string SteamCodePrefix = "steam:code:";

    [HttpGet]
    [EnableRateLimiting("external")]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAccounts(CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Include(a => a.MapRanks)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync(ct);

        // Best-effort enrichment: refresh the Steam persona name + avatar for linked
        // accounts that don't have one yet (idempotent; skipped without an API key).
        var enriched = false;
        foreach (var account in accounts)
        {
            if (string.IsNullOrWhiteSpace(account.Steam64Id))
                continue;
            if (!string.IsNullOrWhiteSpace(account.AvatarUrl))
                continue;

            var summary = await steam.GetPlayerSummariesAsync(account.Steam64Id, ct);
            if (summary is null)
                continue;

            if (!string.IsNullOrWhiteSpace(summary.AvatarFull))
                account.AvatarUrl = summary.AvatarFull;
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
        var account = await db.Accounts.AsNoTracking()
            .Include(a => a.MapRanks)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return NotFound();

        var matches = await db.Matches.AsNoTracking()
            .Where(m => m.AccountId == id)
            .OrderByDescending(m => m.FinishedAt)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();
        var flaggedPlayerMatchIds = await db.MatchPlayers.AsNoTracking()
            .Where(p => matchIds.Contains(p.MatchId) && p.FlaggedAt != null)
            .Select(p => p.MatchId)
            .Distinct()
            .ToHashSetAsync(ct);

        return Ok(matches.Select(m => MatchMapper.ToDto(m, account, flaggedPlayerMatchIds.Contains(m.Id))));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Unlink(int id, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return NotFound(new { error = "Account not found." });

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken ct)
    {
        var accounts = await db.Accounts.ToListAsync(ct);

        var order = request.Order;
        if (order.Count != accounts.Count || order.Distinct().Count() != order.Count)
            return BadRequest(new { error = "Invalid account order." });

        var idSet = accounts.Select(a => a.Id).ToHashSet();
        if (order.Any(id => !idSet.Contains(id)))
            return BadRequest(new { error = "Invalid account order." });

        for (var i = 0; i < order.Count; i++)
        {
            var account = accounts.First(a => a.Id == order[i]);
            account.DisplayOrder = i;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("steam/link")]
    public IActionResult LinkSteam()
    {
        var state = Guid.NewGuid().ToString("N");
        cache.Set(LinkStatePrefix + state, true, TimeSpan.FromMinutes(15));
        return Ok(new { url = openId.BuildLoginUrl(state) });
    }

    [HttpGet("steam/callback")]
    public async Task<IActionResult> SteamCallback([FromQuery] string? state, CancellationToken ct)
    {
        var frontend = openIdOptions.Value.FrontendBaseUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(state) || !cache.TryGetValue(LinkStatePrefix + state, out bool _))
            return Redirect($"{frontend}/accounts#steam=failed");

        cache.Remove(LinkStatePrefix + state);

        var steam64 = await openId.VerifyCallbackAsync(Request.Query, ct);
        if (steam64 is null)
            return Redirect($"{frontend}/accounts#steam=failed");

        var code = Guid.NewGuid().ToString("N");
        cache.Set(SteamCodePrefix + code, steam64, TimeSpan.FromMinutes(10));
        return Redirect($"{frontend}/accounts#steam_code={code}");
    }

    [HttpPost("steam/exchange")]
    [EnableRateLimiting("external")]
    public async Task<IActionResult> ExchangeSteamCode(SteamExchangeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            !cache.TryGetValue(SteamCodePrefix + request.Code, out string? steam64) ||
            steam64 is null)
            return BadRequest(new { error = "Invalid or expired Steam link code." });
        cache.Remove(SteamCodePrefix + request.Code);

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Steam64Id == steam64, ct);
        var created = account is null;

        // Best-effort enrichment with Steam persona name + avatar (no key / failure is non-fatal).
        var summary = await steam.GetPlayerSummariesAsync(steam64, ct);
        var persona = !string.IsNullOrWhiteSpace(summary?.PersonaName) ? summary!.PersonaName : null;

        if (account is null)
        {
            var maxOrder = await db.Accounts
                .Select(a => (int?)a.DisplayOrder)
                .MaxAsync(ct) ?? 0;

            account = db.Accounts.Add(new Account
            {
                Name = persona ?? steam64,
                Steam64Id = steam64,
                AvatarUrl = summary?.AvatarFull,
                DisplayOrder = maxOrder + 1,
                CreatedAt = DateTime.UtcNow,
            }).Entity;
        }
        else
        {
            if (persona is not null)
                account.Name = persona;
            if (!string.IsNullOrWhiteSpace(summary?.AvatarFull))
                account.AvatarUrl = summary.AvatarFull;
        }

        await db.SaveChangesAsync(ct);

        if (created)
            await pendingResolver.AutoAttributeAsync(account!.Id, steam64, ct);

        return NoContent();
    }

    [HttpGet("{id:int}/stats")]
    public async Task<ActionResult<AccountStatsDto>> GetStats(int id, CancellationToken ct)
    {
        var exists = await db.Accounts.AsNoTracking()
            .AnyAsync(a => a.Id == id, ct);
        if (!exists)
            return NotFound();

        return Ok(await BuildStatsAsync([id], ct));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AccountStatsDto>> GetAccountsSummary(CancellationToken ct)
    {
        var accountIds = await db.Accounts.AsNoTracking()
            .Select(a => a.Id)
            .ToListAsync(ct);
        return Ok(await BuildStatsAsync(accountIds, ct));
    }

    private async Task<AccountStatsDto> BuildStatsAsync(IReadOnlyList<int> accountIds, CancellationToken ct)
    {
        var matches = await db.Matches.AsNoTracking()
            .Where(m => accountIds.Contains(m.AccountId) && m.Status == ParseStatus.Parsed)
            .ToListAsync(ct);

        var matchIds = matches.Select(m => m.Id).ToList();

        var players = await db.MatchPlayers.AsNoTracking()
            .Include(p => p.Match)
            .Where(p => matchIds.Contains(p.MatchId))
            .ToListAsync(ct);

        var totalMatches = matches.Count;
        var flaggedPlayerMatches = players.Where(p => p.FlaggedAt is not null).Select(p => p.MatchId).Distinct().Count();

        var cheatingMatchIds = players
            .Where(p => p.FlaggedAt is not null && p.FlagReason == 1)
            .Select(p => p.MatchId)
            .Distinct()
            .ToHashSet();
        var cheaterMatches = matches.Where(m => cheatingMatchIds.Contains(m.Id)).ToList();
        int cheaterWins = 0;
        foreach (var m in cheaterMatches)
        {
            var (our, their) = MatchScore.SplitScores(m.CtScore, m.TScore, m.OurTeamNumber);
            if (our > their) cheaterWins++;
        }

        var flaggedPlayers = players.Count(p => p.FlaggedAt is not null);

        var ownAccountIds = await db.Accounts.AsNoTracking()
            .Where(a => a.Steam64Id != null)
            .Select(a => a.Steam64Id!)
            .ToHashSetAsync(ct);
        var distinctPlayers = players
            .Select(p => p.Steam64Id)
            .Where(id => !ownAccountIds.Contains(id))
            .Distinct()
            .Count();

        var vacBannedIds = await db.PlayerBanInfo.AsNoTracking()
            .Where(c => c.VacBanned)
            .Select(c => c.Steam64Id)
            .ToHashSetAsync(ct);
        var bannedPlayers = players
            .Select(p => p.Steam64Id)
            .Where(id => vacBannedIds.Contains(id) && !ownAccountIds.Contains(id))
            .Distinct()
            .Count();

        var flaggedPlayersList = players
            .Where(p => p.FlaggedAt is not null)
            .GroupBy(p => p.Steam64Id)
            .Select(g =>
            {
                var latest = g.MaxBy(p => p.FlaggedAt) ?? g.First();
                return new FlaggedPlayerDto(
                    g.Key,
                    latest.Name,
                    latest.FlagReason,
                    latest.FlagNote,
                    vacBannedIds.Contains(g.Key),
                    g.Count());
            })
            .OrderByDescending(f => f.VacBanned)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        return new AccountStatsDto(
            totalMatches,
            flaggedPlayerMatches,
            flaggedPlayers,
            bannedPlayers,
            cheaterMatches.Count > 0 ? (double)cheaterWins / cheaterMatches.Count : 0,
            distinctPlayers,
            byMap,
            byMode,
            flaggedPlayersList);
    }
}

public static class MatchMapper
{
    public static MatchDto ToDto(Match match, Account account, bool hasFlaggedPlayer)
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
            match.Source == MatchSource.Upload ? null : match.FinishedAt.ToString("o", CultureInfo.InvariantCulture),
            match.Suspected,
            match.FlaggedAt is not null,
            match.Status.ToString(),
            match.ScoredAt?.ToString("o", CultureInfo.InvariantCulture),
            hasFlaggedPlayer);
    }

    private static RankDto? ResolveRank(Account account, Match match)
        => RankDto.FromCapture(match.Mode, match.OwnRankType, match.OwnRankValue);
}
