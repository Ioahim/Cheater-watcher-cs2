using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services.Ingestion;

public sealed record ShareCodeIngestResult(string Status, Guid? MatchId = null);

/// <summary>
/// Ingests a single share code as a match for an account. Each code is decoded to a
/// unique match identity and deduplicated per account, so a given match can only ever
/// be ingested once.
/// </summary>
public class ShareCodeIngestionService(ParseQueue queue, DemoDownloader downloader, DemoStorage storage)
{
    public virtual async Task<ShareCodeIngestResult> IngestAsync(
        AppDbContext db, int accountId, string shareCode, CancellationToken ct)
    {
        if (!ShareCode.TryDecode(shareCode, out var info))
            return new ShareCodeIngestResult("invalid");

        var matchId = info.MatchId.ToString();
        var exists = await db.Matches.AnyAsync(m => m.AccountId == accountId && m.DemoSourceId == matchId, ct);
        if (exists)
            return new ShareCodeIngestResult("duplicate");

        string demoPath;
        try
        {
            demoPath = await downloader.DownloadDemoAsync(info, storage.Root, ct);
        }
        catch (Exception)
        {
            return new ShareCodeIngestResult("download_failed");
        }

        var match = new Match
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Source = MatchSource.ShareCode,
            DemoFileName = Path.GetFileName(demoPath),
            DemoSourceId = matchId,
            Status = ParseStatus.Pending,
            FinishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        db.Matches.Add(match);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent ingestion of the same (AccountId, DemoSourceId) won the race
            // against the unique index. Treat it as a duplicate rather than surfacing
            // an error that would abort the caller's whole batch.
            return new ShareCodeIngestResult("duplicate");
        }

        await queue.EnqueueAsync(new ParseJob(match.Id, demoPath), ct);
        return new ShareCodeIngestResult("ingested", match.Id);
    }
}
