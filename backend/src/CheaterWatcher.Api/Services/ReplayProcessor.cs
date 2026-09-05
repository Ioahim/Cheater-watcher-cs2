using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services;

/// <summary>
/// Shared replay-processing helpers for the automatic scanner and the manual
/// resolve endpoint.
/// </summary>
public class ReplayProcessor(
    AppDbContext db,
    DemoExtractor extractor,
    DemoInfoReader infoReader,
    RankIngest ranks)
{
    public static string FullPath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public Task<ExtractedDemo> ExtractAsync(string fullPath, CancellationToken ct)
        => extractor.ExtractAsync(fullPath, ct);

    /// <summary>Returns the real .info start time when available, else the file
    /// last-write time (the demo itself carries no wall-clock timestamp).</summary>
    public DateTime ResolveFinishedAt(string fullPath, DateTime fallback)
        => infoReader.TryReadStartTime(fullPath) ?? DateTime.SpecifyKind(fallback, DateTimeKind.Utc).ToUniversalTime();

    /// <summary>
    /// Creates a Parsed match from already-extracted data and attaches it to the
    /// given account. Returns the match, or null if a match (account, hash) exists.
    /// </summary>
    public async Task<Match?> CreateMatchAsync(
        int accountId,
        ExtractedDemo extracted,
        string relative,
        string hash,
        DateTime finishedAt,
        CancellationToken ct)
    {
        var exists = await db.Matches.AnyAsync(m =>
            m.AccountId == accountId && m.DemoSourceId == hash, ct);
        if (exists)
            return null;

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        var match = new Match
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Source = MatchSource.Replay,
            MapName = extracted.MapName,
            Mode = extracted.Mode,
            CtScore = extracted.CtScore,
            TScore = extracted.TScore,
            DemoFileName = relative,
            DemoSourceId = hash,
            Status = ParseStatus.Parsed,
            FinishedAt = finishedAt,
            ParsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            DeleteDemoAfterParse = false,
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);

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

        await ranks.ApplyAsync(account, match, extracted, ct);

        await db.SaveChangesAsync(ct);
        return match;
    }
}
