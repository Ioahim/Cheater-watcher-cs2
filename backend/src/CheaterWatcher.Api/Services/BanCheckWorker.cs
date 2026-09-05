using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

public class BanCheckWorker(
    BanCheckQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<SteamOptions> options,
    ILogger<BanCheckWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SeedInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SeedMissingAsync(stoppingToken);
        var lastSeed = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
                if (DateTime.UtcNow - lastSeed >= SeedInterval)
                {
                    await SeedMissingAsync(stoppingToken);
                    lastSeed = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ban check cycle failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        while (queue.TryDequeue(out var job))
            await ProcessAsync(job, ct);
    }

    // Manually flagged players (Cheating/Suspicious) whose ban info is missing or
    // stale get re-checked on startup and periodically, so bans issued after a flag
    // are eventually picked up.
    private async Task SeedMissingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = DateTime.UtcNow - TimeSpan.FromHours(Math.Max(1, options.Value.BanCacheHours));

            var flaggedIds = await db.MatchPlayers.AsNoTracking()
                .Where(p => p.FlaggedAt != null && (p.FlagReason == 1 || p.FlagReason == 4))
                .Select(p => p.Steam64Id)
                .Distinct()
                .ToListAsync(ct);

            var freshIds = await db.PlayerBanInfo.AsNoTracking()
                .Where(c => c.FetchedAt > cutoff)
                .Select(c => c.Steam64Id)
                .ToHashSetAsync(ct);

            foreach (var steam64Id in flaggedIds.Where(id => !freshIds.Contains(id)))
            {
                await queue.EnqueueAsync(new BanCheckJob(steam64Id), ct);
                logger.LogInformation("Queued ban check for flagged player {Steam64Id}", steam64Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed ban checks");
        }
    }

    private async Task ProcessAsync(BanCheckJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SteamPlayerBanService>();
        var info = await service.GetOrRefreshAsync(job.Steam64Id, ct);
        logger.LogInformation(
            "Ban check for {Steam64Id}: VacBanned={Vac} VAC bans={Vacs} Game bans={Games}",
            job.Steam64Id,
            info?.VacBanned == true,
            info?.NumberOfVACBans ?? 0,
            info?.NumberOfGameBans ?? 0);
    }
}