using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Persistence;

public static class DbSeeder
{
    public static readonly string[] CompetitiveMaps =
    [
        "Cache", "Anubis", "Inferno", "Mirage", "Dust II", "Nuke", "Ancient",
        "Train", "Vertigo", "Overpass", "Boulder", "Fachwerk", "Shelter", "Office", "Italy",
    ];

    // (premierRating, wingmanLevel, compLevels per CompetitiveMaps order)
    private static readonly (int Premier, int Wingman, int[] CompLevels)[] SeedData =
    [
        (18342, 15, [16, 14, 15, 17, 13, 15, 12, 11, 10, 9, 8, 7, 9, 12, 13]),
        (9800,  10, [11, 12, 10, 9,  13, 8,  9,  7,  6,  5, 4, 5, 6, 7,  8]),
        (5230,  6,  [7,  6,  8,  5,  4,  6,  3,  4,  2,  3, 2, 1, 3, 4,  5]),
        (12750, 12, [13, 15, 14, 12, 16, 11, 14, 12, 10, 8, 9, 7, 8, 9,  10]),
        (30450, 18, [18, 18, 18, 18, 17, 18, 18, 18, 17, 16, 15, 14, 16, 17, 18]),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var existing = await db.Accounts.AnyAsync();
        if (existing)
            return;

        var now = DateTime.UtcNow;
        for (var i = 0; i < SeedData.Length; i++)
        {
            var (premier, wingman, compLevels) = SeedData[i];
            var account = new Account
            {
                Name = $"Account {i + 1}",
                PremierRating = premier,
                WingmanLevel = wingman,
                CreatedAt = now,
            };
            for (var m = 0; m < CompetitiveMaps.Length; m++)
                account.MapRanks.Add(new AccountMapRank { Map = CompetitiveMaps[m], Level = compLevels[m] });
            db.Accounts.Add(account);
        }

        await db.SaveChangesAsync();
    }
}
