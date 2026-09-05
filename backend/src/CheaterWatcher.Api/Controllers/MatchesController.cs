using System.Text.Json;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Ingestion;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController(
    AppDbContext db,
    DemoStorage storage,
    ParseQueue queue,
    BanCheckQueue banQueue,
    IOptions<SuspicionOptions> suspicionOptions,
    ILogger<MatchesController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("upload")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(600_000_000)]
    public async Task<ActionResult<UploadResponse>> Upload([FromForm] IFormFile? file, [FromForm] int accountId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (!Path.GetExtension(file.FileName).Equals(".dem", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .dem files are accepted." });

        if (file.Length > storage.MaxUploadBytes)
            return BadRequest(new { error = $"File exceeds the {storage.MaxUploadBytes / (1024 * 1024)} MB limit." });

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null)
            return NotFound(new { error = "Account not found." });

        storage.EnsureRoot();
        var tempPath = Path.Combine(storage.Root, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(fs, ct);
            }

            var hash = await DemoStorage.ComputeSha256Async(tempPath, ct);

            var existing = await db.Matches.FirstOrDefaultAsync(m => m.AccountId == accountId && m.DemoSourceId == hash, ct);
            if (existing is not null)
                return Ok(new UploadResponse(existing.Id, Duplicate: true));

            var freshName = Path.GetFileName(file.FileName);
            var freshPath = Path.Combine(storage.Root, freshName);
            if (System.IO.File.Exists(freshPath))
            {
                var stem = Path.GetFileNameWithoutExtension(freshName);
                var ext = Path.GetExtension(freshName);
                freshPath = Path.Combine(storage.Root, $"{stem}_{hash[..8]}{ext}");
                var attempt = 1;
                while (System.IO.File.Exists(freshPath))
                    freshPath = Path.Combine(storage.Root, $"{stem}_{hash[..8]}_{attempt++}{ext}");
            }
            System.IO.File.Move(tempPath, freshPath);

            var match = new Match
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Source = MatchSource.Upload,
                DemoFileName = Path.GetFileName(freshPath),
                DemoSourceId = hash,
                Status = ParseStatus.Pending,
                FinishedAt = System.IO.File.GetLastWriteTimeUtc(freshPath),
                CreatedAt = DateTime.UtcNow,
                DeleteDemoAfterParse = true,
            };
            db.Matches.Add(match);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // A concurrent identical upload won the race on the unique
                // (AccountId, DemoSourceId) index. Return the already-existing record.
                System.IO.File.Delete(freshPath);
                if (db.Entry(match).State != EntityState.Detached)
                    db.Entry(match).State = EntityState.Detached;
                var dup = await db.Matches.FirstOrDefaultAsync(m => m.AccountId == accountId && m.DemoSourceId == hash, ct);
                if (dup is null)
                    throw;
                return Ok(new UploadResponse(dup.Id, Duplicate: true));
            }

            await queue.EnqueueAsync(new ParseJob(match.Id, freshPath), ct);
            logger.LogInformation("Queued upload {MatchId} ({File})", match.Id, match.DemoFileName);

            return Accepted(new UploadResponse(match.Id, Duplicate: false));
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchStatusDto>> GetStatus(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync(ct);
        if (match is null)
            return NotFound();

        return Ok(new MatchStatusDto(
            match.Id,
            match.Status.ToString(),
            match.ErrorMessage,
            match.Suspected,
            match.FlaggedAt is not null));
    }

    [HttpGet("{id:guid}/players")]
    public async Task<ActionResult<MatchRosterDto>> GetPlayers(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(m => m.Players)
            .Include(m => m.Account)
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync(ct);
        if (match is null)
            return NotFound();

        var threshold = suspicionOptions.Value.Threshold;
        var ownSteam64Id = match.Account?.Steam64Id;

        var playerIds = match.Players.Select(p => p.Steam64Id).ToList();
        var vacBannedIds = await db.PlayerBanInfo.AsNoTracking()
            .Where(c => playerIds.Contains(c.Steam64Id) && c.VacBanned)
            .Select(c => c.Steam64Id)
            .ToHashSetAsync(ct);

        static decimal Kd(MatchPlayerDto p) => p.Kills / Math.Max(1m, p.Deaths);

        return Ok(new MatchRosterDto(
            Ct: match.Players.Where(p => p.TeamNumber == 3)
                .Select(p => ToPlayerDto(p, match.Mode, threshold, vacBannedIds.Contains(p.Steam64Id), p.Steam64Id == ownSteam64Id))
                .OrderByDescending(Kd)
                .ToList(),
            T: match.Players.Where(p => p.TeamNumber == 2)
                .Select(p => ToPlayerDto(p, match.Mode, threshold, vacBannedIds.Contains(p.Steam64Id), p.Steam64Id == ownSteam64Id))
                .OrderByDescending(Kd)
                .ToList(),
            AverageRank: AverageRank(match.Mode, match.Players)));
    }

    private static RankDto? AverageRank(string mode, ICollection<MatchPlayer> players)
    {
        if (mode == "Premier")
        {
            var ratings = players
                .Where(p => p.RankType == CsRankTypes.Premier && p.RankValue is > 0)
                .Select(p => p.RankValue.GetValueOrDefault())
                .ToList();
            return ratings.Count > 0 ? new RankDto("premier", (int)Math.Round(ratings.Average()), null) : null;
        }

        if (mode is "Competitive" or "Wingman")
        {
            var levels = players
                .Where(p => p.RankValue is >= 1 and <= 18)
                .Select(p => p.RankValue.GetValueOrDefault())
                .ToList();
            if (levels.Count == 0)
                return null;
            var kind = mode == "Wingman" ? "wingman" : "competitive";
            return new RankDto(kind, null, (int)Math.Round(levels.Average()));
        }

        return null;
    }

    [HttpPost("{id:guid}/players/{playerId:long}/flag")]
    public async Task<IActionResult> FlagPlayer(Guid id, long playerId, [FromBody] FlagPlayerRequest? body, CancellationToken ct)
    {
        var player = await db.MatchPlayers
            .Include(p => p.Match).ThenInclude(m => m.Account)
            .FirstOrDefaultAsync(p => p.Id == playerId && p.MatchId == id, ct);
        if (player is null)
            return NotFound();

        if (player.Match?.Account is { Steam64Id: not null } account && player.Steam64Id == account.Steam64Id)
            return BadRequest(new { error = "You cannot flag your own account." });

        if (body is { Reason: < 0 or > 4 })
            return BadRequest(new { error = "Invalid flag reason." });

        player.FlaggedAt ??= DateTime.UtcNow;
        player.FlagReason = body?.Reason ?? 1;
        player.FlagNote = body?.Note;
        await db.SaveChangesAsync(ct);

        if (player.FlagReason is 1 or 4)
            await banQueue.EnqueueAsync(new BanCheckJob(player.Steam64Id), ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}/players/{playerId:long}/flag")]
    public async Task<IActionResult> UnflagPlayer(Guid id, long playerId, CancellationToken ct)
    {
        var player = await db.MatchPlayers
            .FirstOrDefaultAsync(p => p.Id == playerId && p.MatchId == id, ct);
        if (player is null)
            return NotFound();

        player.FlaggedAt = null;
        player.FlagReason = 0;
        player.FlagNote = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/flag")]
    public async Task<IActionResult> Flag(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match is null)
            return NotFound();

        match.FlaggedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/flag")]
    public async Task<IActionResult> Unflag(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match is null)
            return NotFound();

        match.FlaggedAt = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MatchPlayerDto ToPlayerDto(MatchPlayer p, string mode, int threshold, bool vacBanned, bool isOwnAccount)
    {
        var reasons = new List<PlayerReasonDto>();
        if (!string.IsNullOrEmpty(p.SuspicionBreakdownJson))
        {
            try
            {
                var rules = JsonSerializer.Deserialize<List<RuleHit>>(p.SuspicionBreakdownJson, JsonOpts) ?? [];
                reasons = rules
                    .Where(r => r.Triggered)
                    .Select(r => new PlayerReasonDto(r.Name, r.Detail))
                    .ToList();
            }
            catch (JsonException)
            {
            }
        }

        return new MatchPlayerDto(
            p.Id,
            p.Name,
            p.Steam64Id,
            p.Kills,
            p.Deaths,
            p.Assists,
            Suspected: p.SuspicionScore is { } score && score >= threshold,
            reasons,
            Flagged: p.FlaggedAt is not null,
            p.FlagReason,
            p.FlagNote,
            RankDto.FromCapture(mode, p.RankType, p.RankValue),
            vacBanned,
            isOwnAccount);
    }
}
