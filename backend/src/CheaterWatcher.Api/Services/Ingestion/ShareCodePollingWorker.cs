using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services.Ingestion;

public class ShareCodePollingWorker(
    ParseQueue queue,
    IServiceScopeFactory scopeFactory,
    SteamWebApiClient steam,
    DemoDownloader downloader,
    IOptions<SteamOptions> options,
    ILogger<ShareCodePollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.PollingIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Share-code polling cycle failed");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<DemoStorage>();

        if (string.IsNullOrWhiteSpace(options.Value.WebApiKey))
            return;

        var accounts = await db.Accounts
            .Where(a => a.Steam64Id != null && a.AuthCode != null)
            .ToListAsync(ct);

        foreach (var account in accounts)
        {
            ct.ThrowIfCancellationRequested();

            // Enrollment requires a user-supplied recent share code as the cursor.
            // Valve's API rejects knowncodes older than ~1 month (HTTP 412), so a
            // synthetic zero code can never work for a fresh account.
            if (string.IsNullOrWhiteSpace(account.LatestShareCode))
            {
                logger.LogInformation("Account {AccountId} has no share code yet; skipping (user must provide a recent one)", account.Id);
                continue;
            }

            var outcome = await steam.GetNextMatchSharingCodeAsync(account.Steam64Id!, account.AuthCode!, account.LatestShareCode!, ct);
            switch (outcome.Result)
            {
                case ShareCodePollResult.NoData:
                    // Nothing new — stay quiet.
                    continue;
                case ShareCodePollResult.NeedsAttention:
                    logger.LogWarning("Account {AccountId} needs attention: supply a fresh share code or regenerate the auth code", account.Id);
                    continue;
                case ShareCodePollResult.Error:
                    logger.LogWarning("Failed to fetch next share code for account {AccountId}", account.Id);
                    continue;
            }

            var next = outcome.SharingCode!;
            if (next == account.LatestShareCode)
                continue;

            if (!ShareCode.TryDecode(next, out var info))
            {
                logger.LogWarning("Undecodable share code for account {AccountId}: {Code}", account.Id, next);
                continue;
            }

            var dedup = await db.Matches.AnyAsync(m => m.AccountId == account.Id && m.DemoSourceId == info.MatchId.ToString(), ct);
            if (dedup)
            {
                account.LatestShareCode = next;
                await db.SaveChangesAsync(ct);
                continue;
            }

            string demoPath;
            try
            {
                demoPath = await downloader.DownloadDemoAsync(info, storage.Root, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Demo download failed for account {AccountId}, match {MatchId}", account.Id, info.MatchId);
                continue;
            }

            var match = new Match
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Source = MatchSource.ShareCode,
                DemoFileName = Path.GetFileName(demoPath),
                DemoSourceId = info.MatchId.ToString(),
                Status = ParseStatus.Pending,
                FinishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            db.Matches.Add(match);
            account.LatestShareCode = next;
            await db.SaveChangesAsync(ct);

            await queue.EnqueueAsync(new ParseJob(match.Id, demoPath), ct);
            logger.LogInformation("Ingested match {MatchId} for account {AccountId}", info.MatchId, account.Id);
        }
    }
}
