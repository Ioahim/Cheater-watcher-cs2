using System.Globalization;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Infrastructure;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Leetify;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController(AppDbContext db, LeetifyService leetify, ISuspicionScorer scorer) : ControllerBase
{
    [HttpGet("{steam64Id}/suspicion")]
    [EnableRateLimiting("external")]
    public async Task<IActionResult> GetSuspicion(string steam64Id, CancellationToken ct)
    {
        var profile = await leetify.GetProfileAsync(steam64Id, ct);
        if (profile is null || !profile.IsPublic || profile.Stats is null)
            return Ok(new { known = false, suspected = false, rules = Array.Empty<object>(), name = profile?.Name });

        var input = new SuspicionInput(
            profile.Stats.Preaim,
            profile.Stats.ReactionTimeMs,
            profile.Stats.AccuracyHead,
            profile.Stats.SprayAccuracy,
            profile.Stats.CounterStrafingGoodShotsRatio,
            profile.Bans is { Count: > 0 });

        var result = scorer.Score(input);
        return Ok(new
        {
            known = true,
            result.Suspected,
            name = profile.Name,
            rules = result.Rules.Select(r => new { r.Name, r.Detail, r.Triggered }),
        });
    }

    [HttpGet("{steam64Id}/matches")]
    public async Task<IActionResult> GetPlayerMatches(string steam64Id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var rows = await db.MatchPlayers.AsNoTracking()
            .Include(p => p.Match).ThenInclude(m => m.Account)
            .Where(p => p.Steam64Id == steam64Id && p.Match.Status == ParseStatus.Parsed)
            .Where(p => userId == null ? p.Match.Account.UserId == null : p.Match.Account.UserId == userId)
            .OrderByDescending(p => p.Match.FinishedAt)
            .Select(p => new
            {
                matchId = p.MatchId,
                map = p.Match.MapName,
                date = p.Match.FinishedAt,
                p.Kills,
                p.Deaths,
                p.Assists,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("{steam64Id}/detail")]
    [EnableRateLimiting("external")]
    public async Task<IActionResult> GetPlayerDetail(string steam64Id, CancellationToken ct)
    {
        var userId = User.TryGetUserId();

        var rows = await db.MatchPlayers.AsNoTracking()
            .Include(p => p.Match).ThenInclude(m => m.Account)
            .Where(p => p.Steam64Id == steam64Id && p.Match.Status == ParseStatus.Parsed)
            .Where(p => userId == null ? p.Match.Account.UserId == null : p.Match.Account.UserId == userId)
            .OrderByDescending(p => p.Match.FinishedAt)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Ok(new PlayerDetailDto(
                steam64Id, "", 0, 0, 0, 0, 0, 0,
                false, 0, null, []));

        var name = rows.First().Name;
        var totalKills = rows.Sum(r => r.Kills);
        var totalDeaths = rows.Sum(r => r.Deaths);
        var totalAssists = rows.Sum(r => r.Assists);

        int onTeam = 0, againstUs = 0;
        foreach (var r in rows)
        {
            if (r.Match.OurTeamNumber is { } ourTeam && r.TeamNumber == ourTeam)
                onTeam++;
            else if (r.Match.OurTeamNumber is not null)
                againstUs++;
        }

        var flagged = rows.Any(r => r.FlaggedAt is not null);
        var flagReason = rows.FirstOrDefault(r => r.FlaggedAt is not null)?.FlagReason ?? 0;
        var flagNote = rows.FirstOrDefault(r => r.FlaggedAt is not null)?.FlagNote;

        var encounters = rows.Select(r =>
        {
            var (ourScore, theirScore) = MatchScore.SplitScores(r.Match.CtScore, r.Match.TScore, r.Match.OurTeamNumber);
            var result = MatchScore.ResultChar(r.Match.OurTeamNumber, ourScore, theirScore);

            return new PlayerEncounterDto(
                r.MatchId,
                MapNames.Display(r.Match.MapName),
                r.Match.Mode,
                r.Match.FinishedAt.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
                result,
                r.Kills,
                r.Deaths,
                r.Assists,
                r.TeamNumber,
                r.FlagReason,
                r.FlagNote);
        }).ToList();

        return Ok(new PlayerDetailDto(
            steam64Id,
            name,
            rows.Count,
            onTeam,
            againstUs,
            totalKills,
            totalDeaths,
            totalAssists,
            flagged,
            flagReason,
            flagNote,
            encounters));
    }
}
