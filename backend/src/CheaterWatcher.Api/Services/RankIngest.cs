using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services;

/// <summary>
/// Applies an account's rank from a parsed match, promoting the account's
/// per-gamemode rank only when this match is the latest one played on that
/// gamemode (so re-parses / out-of-order scans never downgrade a newer rank).
/// Also snapshots the account's own rank at match time onto the match row.
/// </summary>
public class RankIngest(AppDbContext db)
{
    public Task ApplyAsync(Account account, Match match, ExtractedDemo extracted, CancellationToken ct)
    {
        var value = Apply(account, match, extracted);
        return value is { } rank
            ? UpsertMapRankAsync(account.Id, match.MapName, rank, ct)
            : Task.CompletedTask;
    }

    private int? Apply(Account account, Match match, ExtractedDemo extracted)
    {
        if (string.IsNullOrWhiteSpace(account.Steam64Id))
            return null;

        var ours = extracted.Players.FirstOrDefault(p => p.Steam64Id == account.Steam64Id);
        if (ours is null)
            return null;

        match.OurTeamNumber = ours.TeamNumber;
        match.OwnRankType = ours.RankType;
        match.OwnRankValue = ours.RankValue;

        // Manually uploaded demos (recordings, old games) must not promote the
        // account's ranks. The per-match snapshot above is kept for display.
        if (match.Source == MatchSource.Upload)
            return null;

        if (match.Mode == "Premier" && ours.RankType == CsRankTypes.Premier && ours.RankValue is { } premier)
        {
            if (IsLatest(account.Id, "Premier", match))
                account.PremierRating = premier;
            return null;
        }

        if (match.Mode == "Wingman" && ours.RankValue is { } wingman and >= 1 and <= 18)
        {
            if (IsLatest(account.Id, "Wingman", match))
                account.WingmanLevel = wingman;
            return null;
        }

        if (match.Mode == "Competitive" && ours.RankValue is { } comp and >= 1 and <= 18)
        {
            if (IsLatest(account.Id, "Competitive", match, map: match.MapName))
                return comp;
        }

        return null;
    }

    private bool IsLatest(int accountId, string mode, Match current, string? map = null)
    {
        var query = db.Matches.Where(m =>
            m.AccountId == accountId &&
            m.Mode == mode &&
            m.Id != current.Id &&
            m.FinishedAt > current.FinishedAt);
        if (map is not null)
            query = query.Where(m => m.MapName == map);
        return !query.Any();
    }

    private async Task UpsertMapRankAsync(int accountId, string map, int level, CancellationToken ct)
    {
        var row = db.AccountMapRanks.FirstOrDefault(r => r.AccountId == accountId && r.Map == map);
        if (row is null)
        {
            db.AccountMapRanks.Add(new AccountMapRank { AccountId = accountId, Map = map, Level = level });
        }
        else
        {
            row.Level = level;
        }
        await db.SaveChangesAsync(ct);
    }
}
