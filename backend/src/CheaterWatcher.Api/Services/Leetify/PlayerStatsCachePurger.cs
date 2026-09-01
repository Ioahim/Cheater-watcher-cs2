using CheaterWatcher.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services.Leetify;

public class PlayerStatsCachePurger(
    IServiceScopeFactory scopeFactory,
    IOptions<LeetifyOptions> options,
    ILogger<PlayerStatsCachePurger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.CacheHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(ttl, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Leetify cache purge failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PurgeAsync(TimeSpan ttl, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow - ttl;
        var removed = await db.PlayerStatsCache
            .Where(c => c.FetchedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        if (removed > 0)
            logger.LogInformation("Purged {Count} expired Leetify cache entries", removed);
    }
}
