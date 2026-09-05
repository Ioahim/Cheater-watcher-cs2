using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CheaterWatcher.Tests;

public class RankIngestTests
{
    private static AppDbContext NewDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static (ExtractedDemo demo, Match match) NewPremier(string map, short? rankType, int? rankValue)
    {
        var demo = new ExtractedDemo(
            map,
            "Premier",
            13,
            0,
            [new ExtractedPlayer("76000000000000001", "Player", 3, 20, 5, 3, rankType, rankValue)]);
        return (demo, new Match { MapName = map, Mode = "Premier" });
    }

    [Fact]
    public async Task ApplyAsync_OlderPremierMatch_DoesNotDowngradeNewerRating()
    {
        using var db = NewDb(nameof(ApplyAsync_OlderPremierMatch_DoesNotDowngradeNewerRating));
        var account = new Account { Name = "A", Steam64Id = "76000000000000001", PremierRating = 18000 };
        db.Accounts.Add(account);
        db.SaveChanges();

        // Newer match already in DB with a higher rating is excluded from the "latest" check
        // for the older match being ingested now.
        var newer = new Match { AccountId = account.Id, Account = account, MapName = "de_mirage", Mode = "Premier", FinishedAt = DateTime.UtcNow };
        newer.OwnRankType = CsRankTypes.Premier;
        newer.OwnRankValue = 18000;
        db.Matches.Add(newer);
        db.SaveChanges();

        // Ingest an older (chronologically earlier) match with a lower rating.
        var (olderDemo, olderMatch) = NewPremier("de_mirage", CsRankTypes.Premier, 14000);
        olderMatch.AccountId = account.Id;
        olderMatch.FinishedAt = newer.FinishedAt.AddHours(-1);
        db.Matches.Add(olderMatch);
        db.SaveChanges();

        var ingest = new RankIngest(db);
        await ingest.ApplyAsync(account, olderMatch, olderDemo, CancellationToken.None);

        Assert.Equal(18000, account.PremierRating);
    }

    [Fact]
    public async Task ApplyAsync_NewerPremierMatch_PromotesRating()
    {
        using var db = NewDb(nameof(ApplyAsync_NewerPremierMatch_PromotesRating));
        var account = new Account { Name = "A", Steam64Id = "76000000000000001", PremierRating = 14000 };
        db.Accounts.Add(account);
        db.SaveChanges();

        var (movieDemo, match) = NewPremier("de_mirage", CsRankTypes.Premier, 17000);
        match.AccountId = account.Id;
        match.FinishedAt = DateTime.UtcNow;
        db.Matches.Add(match);
        db.SaveChanges();

        var ingest = new RankIngest(db);
        await ingest.ApplyAsync(account, match, movieDemo, CancellationToken.None);

        Assert.Equal(17000, account.PremierRating);
    }

    [Fact]
    public async Task ApplyAsync_SnapshotsOwnRankOntoMatch()
    {
        using var db = NewDb(nameof(ApplyAsync_SnapshotsOwnRankOntoMatch));
        var account = new Account { Name = "A", Steam64Id = "76000000000000001" };
        db.Accounts.Add(account);
        db.SaveChanges();

        var (demo, match) = NewPremier("de_mirage", CsRankTypes.Premier, 15500);
        match.AccountId = account.Id;
        match.FinishedAt = DateTime.UtcNow;
        db.Matches.Add(match);
        db.SaveChanges();

        var ingest = new RankIngest(db);
        await ingest.ApplyAsync(account, match, demo, CancellationToken.None);

        Assert.Equal(CsRankTypes.Premier, match.OwnRankType);
        Assert.Equal(15500, match.OwnRankValue);
        Assert.Equal(3, match.OurTeamNumber);
    }
}