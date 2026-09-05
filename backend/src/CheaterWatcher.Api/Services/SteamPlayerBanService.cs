using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

public class SteamPlayerBanService(
    AppDbContext db,
    SteamWebApiClient steam,
    IOptions<SteamOptions> options,
    ILogger<SteamPlayerBanService> logger)
{
    public async Task<PlayerBanInfo?> GetOrRefreshAsync(string steam64Id, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.BanCacheHours));

        var existing = await db.PlayerBanInfo.FirstOrDefaultAsync(c => c.Steam64Id == steam64Id, ct);
        if (existing is { } hit && now - hit.FetchedAt < ttl)
            return hit;

        var ban = await steam.GetPlayerBansAsync(steam64Id, ct);
        if (ban is null)
        {
            if (existing is null)
                logger.LogWarning("Steam ban fetch failed for {Steam64Id}", steam64Id);
            return existing;
        }

        if (existing is null)
        {
            db.PlayerBanInfo.Add(new PlayerBanInfo
            {
                Steam64Id = steam64Id,
                CommunityBanned = ban.CommunityBanned,
                VacBanned = ban.VacBanned,
                NumberOfVACBans = ban.NumberOfVACBans,
                NumberOfGameBans = ban.NumberOfGameBans,
                DaysSinceLastBan = ban.DaysSinceLastBan,
                EconomyBan = ban.EconomyBan,
                FetchedAt = now,
            });
        }
        else
        {
            existing.CommunityBanned = ban.CommunityBanned;
            existing.VacBanned = ban.VacBanned;
            existing.NumberOfVACBans = ban.NumberOfVACBans;
            existing.NumberOfGameBans = ban.NumberOfGameBans;
            existing.DaysSinceLastBan = ban.DaysSinceLastBan;
            existing.EconomyBan = ban.EconomyBan;
            existing.FetchedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent workers raced on the same fresh Steam64Id (PK insert). Both
            // carried the same data, so the loser can drop its write safely.
        }

        return existing is null
            ? await db.PlayerBanInfo.AsNoTracking().FirstOrDefaultAsync(c => c.Steam64Id == steam64Id, ct)
            : existing;
    }
}