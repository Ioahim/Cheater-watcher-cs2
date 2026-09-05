using System.Text.Json;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CheaterWatcher.Tests;

public class PendingReplayResolverTests
{
    private static AppDbContext NewDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static PendingReplay NewPending(string hash, params string[] steam64Ids) => new()
    {
        Id = Guid.NewGuid(),
        FileName = $"{hash}.dem",
        RelativePath = $"{hash}.dem",
        FileHash = hash,
        MapName = "de_inferno",
        Mode = "Premier",
        DiscoveredAt = DateTime.UtcNow,
        Status = PendingReplayStatus.Pending,
        PlayerSteamIdsJson = JsonSerializer.Serialize(steam64Ids),
    };

    private static Account NewAccount(int order, string steam64Id) => new()
    {
        Name = $"A{order}",
        Steam64Id = steam64Id,
        DisplayOrder = order,
    };

    [Fact]
    public async Task FindCandidates_NewAccountSoleInReplay_ReturnsPending()
    {
        using var db = NewDb(nameof(FindCandidates_NewAccountSoleInReplay_ReturnsPending));
        var account = NewAccount(1, "76000000000000001");
        db.Accounts.Add(account);
        db.PendingReplays.Add(NewPending("hashA", "76000000000000001", "76000000000000002"));
        db.SaveChanges();

        var candidates = await PendingReplayResolver.FindCandidatesAsync(db, account.Id, account.Steam64Id!, CancellationToken.None);

        Assert.Single(candidates);
    }

    [Fact]
    public async Task FindCandidates_AnotherLinkedAccountAlsoInReplay_Skips()
    {
        using var db = NewDb(nameof(FindCandidates_AnotherLinkedAccountAlsoInReplay_Skips));
        var newAccount = NewAccount(1, "76000000000000001");
        var otherAccount = NewAccount(2, "76000000000000002");
        db.Accounts.AddRange(newAccount, otherAccount);
        db.PendingReplays.Add(NewPending("hashB", "76000000000000001", "76000000000000002"));
        db.SaveChanges();

        var candidates = await PendingReplayResolver.FindCandidatesAsync(db, newAccount.Id, newAccount.Steam64Id!, CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task FindCandidates_AccountNotInReplay_Skips()
    {
        using var db = NewDb(nameof(FindCandidates_AccountNotInReplay_Skips));
        var account = NewAccount(1, "76000000000000001");
        db.Accounts.Add(account);
        db.PendingReplays.Add(NewPending("hashC", "76000000000000002", "76000000000000003"));
        db.SaveChanges();

        var candidates = await PendingReplayResolver.FindCandidatesAsync(db, account.Id, account.Steam64Id!, CancellationToken.None);

        Assert.Empty(candidates);
    }
}