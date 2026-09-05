using System.Text.Json;
using CheaterWatcher.Api.Contracts;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Controllers;

[ApiController]
[Route("api/replays")]
public class ReplaysController(
    AppDbContext db,
    ReplayProcessor processor,
    ReplayScanner scanner,
    ScoreQueue scoreQueue,
    ReplayEnvService env,
    IOptions<ReplayScanOptions> scanOptions) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<ReplaySettingsDto>> GetSettings(CancellationToken ct)
    {
        var settings = await ReplaySettingsStore.GetOrCreateAsync(db, ct);
        var restartRequired = !string.IsNullOrWhiteSpace(settings.HostPath) &&
                              settings.HostPath != env.StartupEnvPath;

        return Ok(new ReplaySettingsDto(
            !string.IsNullOrWhiteSpace(settings.HostPath),
            settings.HostPath,
            scanOptions.Value.RootPath,
            scanOptions.Value.DefaultScanIntervalMinutes,
            restartRequired,
            settings.LastScanAt,
            settings.LastScanNew,
            settings.LastScanAttributed,
            settings.LastScanPending,
            settings.LastScanError));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<SaveReplayPathResult>> UpdateSettings(UpdateReplaySettingsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Provide a replays folder path." });

        var path = request.Path.Trim().Trim('"').Replace('\\', '/');
        var settings = await ReplaySettingsStore.GetOrCreateAsync(db, ct);
        var restartRequired = path != env.StartupEnvPath;
        var saved = env.WriteEnvPath(path);
        settings.HostPath = path;
        await db.SaveChangesAsync(ct);

        return Ok(new SaveReplayPathResult(saved, restartRequired, env.CanWriteEnv, path));
    }

    [HttpPost("scan")]
    public IActionResult ScanNow()
    {
        scanner.RequestScan();
        return Accepted();
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PendingReplayDto>>> GetPending(CancellationToken ct)
    {
        var linkedIds = await db.Accounts
            .Where(a => a.Steam64Id != null)
            .Select(a => new { a.Id, a.Steam64Id })
            .ToListAsync(ct);

        var pending = await db.PendingReplays
            .Where(p => p.Status == PendingReplayStatus.Pending)
            .OrderByDescending(p => p.DiscoveredAt)
            .ToListAsync(ct);

        var result = new List<PendingReplayDto>();
        foreach (var p in pending)
        {
            var steamIds = JsonSerializer.Deserialize<List<string>>(p.PlayerSteamIdsJson) ?? [];
            var names = JsonSerializer.Deserialize<List<string>>(p.PlayerNamesJson) ?? [];
            var players = steamIds
                .Select((sid, i) => new PendingReplayPlayerDto(
                    sid,
                    i < names.Count ? names[i] : sid,
                    linkedIds.Any(a => a.Steam64Id == sid)))
                .ToList();

            var inMatchAccounts = linkedIds
                .Where(a => a.Steam64Id is not null && steamIds.Contains(a.Steam64Id!))
                .Select(a => a.Id)
                .ToList();

            result.Add(new PendingReplayDto(
                p.Id,
                p.FileName,
                p.MapName,
                p.Mode,
                p.DiscoveredAt,
                players,
                inMatchAccounts));
        }

        return Ok(result);
    }

    [HttpPost("pending/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolvePendingReplayRequest request, CancellationToken ct)
    {
        var pending = await db.PendingReplays
            .FirstOrDefaultAsync(p => p.Id == id && p.Status == PendingReplayStatus.Pending, ct);
        if (pending is null)
            return NotFound(new { error = "Pending replay not found." });

        if (request.Dismiss is true)
        {
            pending.Status = PendingReplayStatus.Dismissed;
            pending.ResolvedAccountId = null;
            await db.SaveChangesAsync(ct);
            return NoContent();
        }

        if (request.AccountId is not { } accountId)
            return BadRequest(new { error = "Provide an accountId or dismiss." });

        var accountExists = await db.Accounts.AnyAsync(a => a.Id == accountId, ct);
        if (!accountExists)
            return NotFound(new { error = "Account not found." });

        var fullPath = ReplayProcessor.FullPath(scanOptions.Value.RootPath, pending.RelativePath);
        if (!System.IO.File.Exists(fullPath))
            return BadRequest(new { error = $"Replay file no longer exists: {pending.RelativePath}" });

        ExtractedDemo extracted;
        try
        {
            extracted = await processor.ExtractAsync(fullPath, ct);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse replay: {ex.Message}" });
        }

        var finishedAt = processor.ResolveFinishedAt(fullPath, System.IO.File.GetLastWriteTimeUtc(fullPath));
        var match = await processor.CreateMatchAsync(accountId, extracted, pending.RelativePath, pending.FileHash, finishedAt, ct);
        if (match is not null)
            await scoreQueue.EnqueueAsync(new ScoreJob(match.Id), ct);

        pending.Status = PendingReplayStatus.Resolved;
        pending.ResolvedAccountId = accountId;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
