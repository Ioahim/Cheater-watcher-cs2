using System.Security.Claims;
using CheaterWatcher.Api.Controllers;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheaterWatcher.Tests;

/// <summary>
/// Regression tests for the stale polling-cursor bug: the account's LatestShareCode
/// must only advance once a share code actually ingests (or is a known duplicate).
/// Persisting it up front could leave Steam polling pointed at an undecodable or
/// failing code.
/// </summary>
public class ShareCodeStaleCursorTests
{
    private sealed class FakeShareIngestion(ShareCodeIngestResult result)
        : ShareCodeIngestionService(null!, null!, null!)
    {
        public override Task<ShareCodeIngestResult> IngestAsync(
            AppDbContext db, int accountId, string shareCode, CancellationToken ct)
            => Task.FromResult(result);
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"sharecode_stale_{name}_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts);
    }

    private static async Task<Account> SeedOwnedAccount(AppDbContext db)
    {
        var account = new Account { Name = "Owned", UserId = 1, CreatedAt = DateTime.UtcNow };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static AccountsController CreateController(
        AppDbContext db,
        ShareCodeIngestionService ingestion)
    {
        var controller = new AccountsController(
            db,
            ingestion,
            new SteamWebApiClient(new HttpClient(), Options.Create(new SteamOptions())))
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "1")])),
                },
            },
        };
        return controller;
    }

    [Fact]
    public async Task UpdateCredentials_does_not_advance_cursor_when_ingest_fails_invalid()
    {
        using var db = NewDb();
        var account = await SeedOwnedAccount(db);
        var ctrl = CreateController(db, new FakeShareIngestion(new ShareCodeIngestResult("invalid")));

        var result = await ctrl.UpdateCredentials(
            account.Id, new("76561198000000000", "authcode", "NOT_A_REAL_SHARE_CODE"), CancellationToken.None);

        Assert.IsAssignableFrom<IActionResult>(result);
        await db.Entry(account).ReloadAsync();
        Assert.Null(account.LatestShareCode);
    }

    [Fact]
    public async Task UpdateCredentials_keeps_previous_cursor_when_ingest_fails()
    {
        using var db = NewDb();
        var account = await SeedOwnedAccount(db);
        account.LatestShareCode = "SOME-EXISTING-CURSOR";
        await db.SaveChangesAsync();
        var ctrl = CreateController(db, new FakeShareIngestion(new ShareCodeIngestResult("download_failed")));

        await ctrl.UpdateCredentials(
            account.Id, new("76561198000000000", "authcode", "NEW-BUT-FAILING-CODE"), CancellationToken.None);

        await db.Entry(account).ReloadAsync();
        Assert.Equal("SOME-EXISTING-CURSOR", account.LatestShareCode);
    }

    [Fact]
    public async Task UpdateCredentials_advances_cursor_when_ingest_succeeds()
    {
        using var db = NewDb();
        var account = await SeedOwnedAccount(db);
        var ctrl = CreateController(db, new FakeShareIngestion(new ShareCodeIngestResult("ingested")));

        await ctrl.UpdateCredentials(
            account.Id, new("76561198000000000", "authcode", "GOOD-SHARE-CODE"), CancellationToken.None);

        await db.Entry(account).ReloadAsync();
        Assert.Equal("GOOD-SHARE-CODE", account.LatestShareCode);
    }
}
