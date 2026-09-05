using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services;

public class ParseWorker(
    ParseQueue queue,
    IServiceScopeFactory scopeFactory,
    DemoExtractor extractor,
    DemoStorage storage,
    ILogger<ParseWorker> logger) : BackgroundService{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error processing match {MatchId}", job.MatchId);
                await MarkFailedAsync(job, ex.Message, CancellationToken.None);
            }
        }
    }

    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.Matches
                .Where(m => m.Status == ParseStatus.Pending)
                .Select(m => new { m.Id, m.DemoFileName })
                .ToListAsync(ct);

            foreach (var p in pending)
            {
                if (string.IsNullOrWhiteSpace(p.DemoFileName))
                {
                    await MarkFailedAsync(new ParseJob(p.Id, ""), "Demo file name is missing.", ct);
                    continue;
                }
                var demoPath = Path.Combine(storage.Root, p.DemoFileName);
                if (!System.IO.File.Exists(demoPath))
                {
                    // The demo file is gone (e.g. demo volume wiped). Marking it Failed
                    // stops it being re-scanned (and stuck Pending forever) on every restart.
                    await MarkFailedAsync(new ParseJob(p.Id, demoPath), $"Demo file not found on disk: {p.DemoFileName}", ct);
                    continue;
                }
                await queue.EnqueueAsync(new ParseJob(p.Id, demoPath), ct);
                logger.LogInformation("Requeued unparsed match {MatchId} after restart", p.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to requeue pending matches on startup");
        }
    }

    private async Task ProcessJobAsync(ParseJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suspicion = scope.ServiceProvider.GetRequiredService<MatchSuspicionService>();
        var ranks = scope.ServiceProvider.GetRequiredService<RankIngest>();
        var infoReader = scope.ServiceProvider.GetRequiredService<DemoInfoReader>();

        var match = await db.Matches.Include(m => m.Players).FirstOrDefaultAsync(m => m.Id == job.MatchId, ct);
        if (match is null)
        {
            logger.LogWarning("Parse job for missing match {MatchId}", job.MatchId);
            return;
        }

        ExtractedDemo extracted;
        try
        {
            extracted = await extractor.ExtractAsync(job.DemoPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo extraction failed for {MatchId} ({Path})", job.MatchId, job.DemoPath);
            match.Status = ParseStatus.Failed;
            match.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(ct);
            return;
        }

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == match.AccountId, ct);

        match.MapName = extracted.MapName;
        match.Mode = extracted.Mode;
        match.CtScore = extracted.CtScore;
        match.TScore = extracted.TScore;
        match.Status = ParseStatus.Parsed;
        match.ParsedAt = DateTime.UtcNow;
        match.ErrorMessage = null;
        if (infoReader.TryReadStartTime(job.DemoPath) is { } startTime)
            match.FinishedAt = startTime;

        match.Players.Clear();
        foreach (var p in extracted.Players)
        {
            match.Players.Add(new MatchPlayer
            {
                Steam64Id = p.Steam64Id,
                Name = p.Name,
                TeamNumber = p.TeamNumber,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                RankType = p.RankType,
                RankValue = p.RankValue,
            });
        }

        if (account is not null)
            await ranks.ApplyAsync(account, match, extracted, ct);

        await db.SaveChangesAsync(ct);

        if (match.DeleteDemoAfterParse)
            TryDeleteDemo(job.DemoPath);

        await suspicion.ScoreMatchAsync(db, match, ct);
    }

    private void TryDeleteDemo(string demoPath)
    {
        try
        {
            if (System.IO.File.Exists(demoPath))
                System.IO.File.Delete(demoPath);
        }
        catch (Exception ex)
        {
            // Deleting is best-effort; a leftover demo is harmless (and re-queued only if Pending).
            logger.LogWarning(ex, "Could not delete demo {Path} after parse", demoPath);
        }
    }

    private async Task MarkFailedAsync(ParseJob job, string message, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == job.MatchId, ct);
            if (match is not null)
            {
                match.Status = ParseStatus.Failed;
                match.ErrorMessage = message;
                await db.SaveChangesAsync(ct);
            }
        }
        catch
        {
        }
    }
}
