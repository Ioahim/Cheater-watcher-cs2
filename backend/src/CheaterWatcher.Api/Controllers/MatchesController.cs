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
[Route("api/matches")]
public class MatchesController(
    AppDbContext db,
    DemoStorage storage,
    ParseQueue queue,
    ShareCodeIngestionService shareIngestion,
    IOptions<SuspicionOptions> suspicionOptions,
    ILogger<MatchesController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("upload")]
    [Authorize]
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

        var userId = User.TryGetUserId();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null || account.UserId != userId)
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

            var finalName = Path.GetFileName(file.FileName);
            var finalPath = Path.Combine(storage.Root, finalName);
            if (System.IO.File.Exists(finalPath))
            {
                var stem = Path.GetFileNameWithoutExtension(finalName);
                var ext = Path.GetExtension(finalName);
                finalPath = Path.Combine(storage.Root, $"{stem}_{hash[..8]}{ext}");
                var attempt = 1;
                while (System.IO.File.Exists(finalPath))
                    finalPath = Path.Combine(storage.Root, $"{stem}_{hash[..8]}_{attempt++}{ext}");
            }
            System.IO.File.Move(tempPath, finalPath);

            var match = new Match
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Source = MatchSource.Upload,
                DemoFileName = Path.GetFileName(finalPath),
                DemoSourceId = hash,
                Status = ParseStatus.Pending,
                FinishedAt = System.IO.File.GetLastWriteTimeUtc(finalPath),
                CreatedAt = DateTime.UtcNow,
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
                System.IO.File.Delete(finalPath);
                if (db.Entry(match).State != EntityState.Detached)
                    db.Entry(match).State = EntityState.Detached;
                var dup = await db.Matches.FirstOrDefaultAsync(m => m.AccountId == accountId && m.DemoSourceId == hash, ct);
                if (dup is null)
                    throw;
                return Ok(new UploadResponse(dup.Id, Duplicate: true));
            }

            await queue.EnqueueAsync(new ParseJob(match.Id, finalPath), ct);
            logger.LogInformation("Queued upload {MatchId} ({File})", match.Id, match.DemoFileName);

            return Accepted(new UploadResponse(match.Id, Duplicate: false));
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [Authorize]
    [EnableRateLimiting("share")]
    [HttpPost("share")]
    public async Task<ActionResult<AddShareCodeResponse>> AddShareCode(AddShareCodeRequest request, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var code = request.ShareCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "No share code provided." });

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct);
        if (account is null || account.UserId != userId)
            return NotFound(new { error = "Account not found." });

        var result = await shareIngestion.IngestAsync(db, account.Id, code, ct);
        return Ok(new AddShareCodeResponse(result.Status, result.MatchId));
    }

    // Guests may read only matches of ownerless demo accounts; users read their own.
    private System.Linq.Expressions.Expression<Func<Match, bool>> ReadableMatches()
    {
        var userId = User.TryGetUserId();
        return m => userId == null ? m.Account.UserId == null : m.Account.UserId == userId;
    }

    // Mutations require an authenticated user who owns the account.
    private System.Linq.Expressions.Expression<Func<Match, bool>> OwnedMatches() =>
        m => m.Account.UserId == User.TryGetUserId();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchStatusDto>> GetStatus(Guid id, CancellationToken ct)
    {
        var match = await db.Matches.AsNoTracking()
            .Include(m => m.Account)
            .Where(m => m.Id == id)
            .Where(ReadableMatches())
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
            .Include(m => m.Account)
            .Include(m => m.Players)
            .Where(m => m.Id == id)
            .Where(ReadableMatches())
            .FirstOrDefaultAsync(ct);
        if (match is null)
            return NotFound();

        var threshold = suspicionOptions.Value.Threshold;

        static decimal Kd(MatchPlayerDto p) => p.Kills / Math.Max(1m, p.Deaths);

        return Ok(new MatchRosterDto(
            Ct: match.Players.Where(p => p.TeamNumber == 3)
                .Select(p => ToPlayerDto(p, match.Mode, threshold))
                .OrderByDescending(Kd)
                .ToList(),
            T: match.Players.Where(p => p.TeamNumber == 2)
                .Select(p => ToPlayerDto(p, match.Mode, threshold))
                .OrderByDescending(Kd)
                .ToList()));
    }

    [Authorize]
    [HttpPost("{id:guid}/players/{playerId:long}/flag")]
    public async Task<IActionResult> FlagPlayer(Guid id, long playerId, [FromBody] FlagPlayerRequest? body, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var player = await db.MatchPlayers
            .Include(p => p.Match).ThenInclude(m => m.Account)
            .FirstOrDefaultAsync(p => p.Id == playerId && p.MatchId == id && p.Match.Account.UserId == userId, ct);
        if (player is null)
            return NotFound();

        if (body is { Reason: < 0 or > 4 })
            return BadRequest(new { error = "Invalid flag reason." });

        player.FlaggedAt ??= DateTime.UtcNow;
        player.FlagReason = body?.Reason ?? 1;
        player.FlagNote = body?.Note;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:guid}/players/{playerId:long}/flag")]
    public async Task<IActionResult> UnflagPlayer(Guid id, long playerId, CancellationToken ct)
    {
        var userId = User.TryGetUserId();
        var player = await db.MatchPlayers
            .Include(p => p.Match).ThenInclude(m => m.Account)
            .FirstOrDefaultAsync(p => p.Id == playerId && p.MatchId == id && p.Match.Account.UserId == userId, ct);
        if (player is null)
            return NotFound();

        player.FlaggedAt = null;
        player.FlagReason = 0;
        player.FlagNote = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/flag")]
    public async Task<IActionResult> Flag(Guid id, CancellationToken ct)
    {
        var match = await db.Matches
            .Include(m => m.Account)
            .Where(OwnedMatches())
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match is null)
            return NotFound();

        match.FlaggedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:guid}/flag")]
    public async Task<IActionResult> Unflag(Guid id, CancellationToken ct)
    {
        var match = await db.Matches
            .Include(m => m.Account)
            .Where(OwnedMatches())
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (match is null)
            return NotFound();

        match.FlaggedAt = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MatchPlayerDto ToPlayerDto(MatchPlayer p, string mode, int threshold)
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
            RankDto.FromCapture(mode, p.RankType, p.RankValue));
    }
}
