using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services;

public class ScoreWorker(
    ScoreQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ScoreWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnscoredAsync(stoppingToken);

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
                logger.LogError(ex, "Unhandled error scoring match {MatchId}", job.MatchId);
            }
        }
    }

    private async Task RequeueUnscoredAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unscored = await db.Matches
                .Where(m => m.Status == ParseStatus.Parsed && m.ScoredAt == null)
                .Select(m => m.Id)
                .ToListAsync(ct);

            foreach (var matchId in unscored)
            {
                await queue.EnqueueAsync(new ScoreJob(matchId), ct);
                logger.LogInformation("Requeued unscored match {MatchId} after restart", matchId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to requeue unscored matches on startup");
        }
    }

    private async Task ProcessJobAsync(ScoreJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suspicion = scope.ServiceProvider.GetRequiredService<MatchSuspicionService>();

        var match = await db.Matches.FirstOrDefaultAsync(m => m.Id == job.MatchId, ct);
        if (match is null)
        {
            logger.LogWarning("Score job for missing match {MatchId}", job.MatchId);
            return;
        }

        await suspicion.ScoreMatchAsync(db, match, ct);
    }
}