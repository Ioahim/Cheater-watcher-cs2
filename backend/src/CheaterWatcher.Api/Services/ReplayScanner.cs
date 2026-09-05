using System.Text.Json;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

public class ReplayScanner(
    IServiceScopeFactory scopeFactory,
    IOptions<ReplayScanOptions> scanOptions,
    ScoreQueue scoreQueue,
    ILogger<ReplayScanner> logger) : BackgroundService
{
    private readonly SemaphoreSlim _trigger = new(0);

    public void RequestScan()
    {
        try { _trigger.Release(); } catch (SemaphoreFullException) { }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Replay scan failed");
            }

            var interval = TimeSpan.FromMinutes(scanOptions.Value.DefaultScanIntervalMinutes);
            try
            {
                await _trigger.WaitAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<ReplayProcessor>();

        var settings = await ReplaySettingsStore.GetOrCreateAsync(db, ct);
        var effectiveRoot = scanOptions.Value.RootPath;

        if (!Directory.Exists(effectiveRoot))
        {
            settings.LastScanAt = DateTime.UtcNow;
            settings.LastScanNew = 0;
            settings.LastScanAttributed = 0;
            settings.LastScanPending = 0;
            settings.LastScanError = $"Replays folder not found at {effectiveRoot}. Set your replays path once in the app and restart.";
            await db.SaveChangesAsync(ct);
            return;
        }

        var knownHashes = await db.ProcessedReplays
            .Select(p => p.FileHash)
            .ToHashSetAsync(ct);

        var files = Directory.EnumerateFiles(effectiveRoot, "*.dem", SearchOption.AllDirectories).ToList();
        var newCount = 0;
        var attributedCount = 0;
        var pendingCount = 0;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
                break;

            var info = new FileInfo(file);
            if (info.Length == 0)
                continue;

            var hash = await DemoStorage.ComputeSha256Async(file, ct);
            if (knownHashes.Contains(hash))
                continue;

            var relative = Path.GetRelativePath(effectiveRoot, file).Replace('\\', '/');
            var outcome = await ProcessFileAsync(db, processor, effectiveRoot, relative, hash, info, ct);

            if (outcome == FileOutcome.Attributed) attributedCount++;
            else if (outcome == FileOutcome.Pending) pendingCount++;
            newCount++;
        }

        settings.LastScanAt = DateTime.UtcNow;
        settings.LastScanNew = newCount;
        settings.LastScanAttributed = attributedCount;
        settings.LastScanPending = pendingCount;
        settings.LastScanError = null;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Replay scan complete: {New} new, {Attr} auto-attributed, {Pending} pending decisions",
            newCount, attributedCount, pendingCount);
    }

    private enum FileOutcome { New, Attributed, Pending, Failed }

    private async Task<FileOutcome> ProcessFileAsync(
        AppDbContext db,
        ReplayProcessor processor,
        string root,
        string relative,
        string hash,
        FileInfo info,
        CancellationToken ct)
    {
        var fullPath = ReplayProcessor.FullPath(root, relative);

        ExtractedDemo extracted;
        try
        {
            extracted = await processor.ExtractAsync(fullPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse replay {Path}", fullPath);
            // Record the file as processed so a corrupt/broken demo isn't re-parsed
            // (and re-hitting Leetify) on every scan cycle.
            await MarkProcessedAsync(db, relative, hash, info, ct);
            return FileOutcome.Failed;
        }

        var players = extracted.Players;
        var linkedIds = await db.Accounts
            .Where(a => a.Steam64Id != null)
            .Select(a => new { a.Id, a.Steam64Id })
            .ToListAsync(ct);

        var presentLinked = linkedIds
            .Where(a => !string.IsNullOrWhiteSpace(a.Steam64Id) && players.Any(p => p.Steam64Id == a.Steam64Id))
            .ToList();

        // Exactly one linked account in the match -> auto-attribute.
        if (presentLinked.Count == 1)
        {
            var finishedAt = processor.ResolveFinishedAt(fullPath, info.LastWriteTimeUtc);
            var match = await processor.CreateMatchAsync(presentLinked[0].Id, extracted, relative, hash, finishedAt, ct);
            if (match is not null)
                await scoreQueue.EnqueueAsync(new ScoreJob(match.Id), ct);

            await MarkProcessedAsync(db, relative, hash, info, ct);
            return FileOutcome.Attributed;
        }

        // Zero or multiple linked accounts -> surface a pending decision.
        var pending = await db.PendingReplays
            .FirstOrDefaultAsync(p => p.FileHash == hash && p.Status == PendingReplayStatus.Pending, ct);
        if (pending is null)
        {
            db.PendingReplays.Add(new PendingReplay
            {
                Id = Guid.NewGuid(),
                FileName = info.Name,
                RelativePath = relative,
                FileHash = hash,
                FileSize = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                MapName = extracted.MapName,
                Mode = extracted.Mode,
                DiscoveredAt = DateTime.UtcNow,
                Status = PendingReplayStatus.Pending,
                PlayerSteamIdsJson = JsonSerializer.Serialize(players.Select(p => p.Steam64Id)),
                PlayerNamesJson = JsonSerializer.Serialize(players.Select(p => p.Name)),
            });
            await db.SaveChangesAsync(ct);
        }

        await MarkProcessedAsync(db, relative, hash, info, ct);
        return FileOutcome.Pending;
    }

    private async Task MarkProcessedAsync(
        AppDbContext db, string relative, string hash, FileInfo info, CancellationToken ct)
    {
        var row = await db.ProcessedReplays.FirstOrDefaultAsync(p => p.FileHash == hash, ct);
        if (row is null)
        {
            db.ProcessedReplays.Add(new ProcessedReplay
            {
                FileHash = hash,
                FileName = info.Name,
                RelativePath = relative,
                FileSize = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc,
                ProcessedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }
        else
        {
            row.RelativePath = relative;
            row.FileName = info.Name;
            row.FileSize = info.Length;
            row.LastWriteTimeUtc = info.LastWriteTimeUtc;
            row.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
