using System.Text.Json;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

/// <summary>
/// Auto-attributes pending replay decisions to an account right after it gets
/// Steam-linked, when that account is the only linked account present in the
/// replay. Mirrors the scanner's "exactly one linked account -> attribute" rule.
/// </summary>
public class PendingReplayResolver(
    AppDbContext db,
    ReplayProcessor processor,
    ScoreQueue scoreQueue,
    IOptions<ReplayScanOptions> scanOptions)
{
    /// <summary>Pending replays that should be attributed to the given account:
    /// the account's Steam64Id must be among the players and no other linked
    /// account may be present in the same replay.</summary>
    public static async Task<List<PendingReplay>> FindCandidatesAsync(
        AppDbContext db, int accountId, string steam64Id, CancellationToken ct)
    {
        var linked = await db.Accounts
            .Where(a => a.Steam64Id != null)
            .Select(a => new { a.Id, a.Steam64Id })
            .ToListAsync(ct);

        var pending = await db.PendingReplays
            .Where(p => p.Status == PendingReplayStatus.Pending)
            .ToListAsync(ct);

        var candidates = new List<PendingReplay>();
        foreach (var p in pending)
        {
            var steamIds = JsonSerializer.Deserialize<List<string>>(p.PlayerSteamIdsJson) ?? [];
            if (!steamIds.Contains(steam64Id))
                continue;

            var otherLinkedPresent = linked.Any(a =>
                a.Id != accountId &&
                !string.IsNullOrWhiteSpace(a.Steam64Id) &&
                steamIds.Contains(a.Steam64Id!));
            if (otherLinkedPresent)
                continue;

            candidates.Add(p);
        }

        return candidates;
    }

    /// <summary>Attaches the pending replay to the given account (extract, create
    /// match, score suspicion) exactly like the manual resolve endpoint.</summary>
    public async Task ResolveAsync(PendingReplay pending, int accountId, CancellationToken ct)
    {
        var fullPath = ReplayProcessor.FullPath(scanOptions.Value.RootPath, pending.RelativePath);
        if (!System.IO.File.Exists(fullPath))
            return;

        var extracted = await processor.ExtractAsync(fullPath, ct);
        var finishedAt = processor.ResolveFinishedAt(fullPath, pending.LastWriteTimeUtc);
        var match = await processor.CreateMatchAsync(accountId, extracted, pending.RelativePath, pending.FileHash, finishedAt, ct);
        if (match is not null)
            await scoreQueue.EnqueueAsync(new ScoreJob(match.Id), ct);

        pending.Status = PendingReplayStatus.Resolved;
        pending.ResolvedAccountId = accountId;
    }

    public async Task AutoAttributeAsync(int accountId, string steam64Id, CancellationToken ct)
    {
        var candidates = await FindCandidatesAsync(db, accountId, steam64Id, ct);
        if (candidates.Count == 0)
            return;

        foreach (var pending in candidates)
            await ResolveAsync(pending, accountId, ct);

        await db.SaveChangesAsync(ct);
    }
}