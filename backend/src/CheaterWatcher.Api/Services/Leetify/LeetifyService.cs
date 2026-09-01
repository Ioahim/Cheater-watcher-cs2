using System.Text.Json;
using System.Text.Json.Serialization;
using CheaterWatcher.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services.Leetify;

public class LeetifyService(AppDbContext db, LeetifyClient client, IOptions<LeetifyOptions> options, ILogger<LeetifyService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<LeetifyProfile?> GetProfileAsync(string steam64Id, CancellationToken ct = default)
    {
        var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.CacheHours));

        var cached = await db.PlayerStatsCache.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Steam64Id == steam64Id, ct);
        if (cached is { } hit && DateTime.UtcNow - hit.FetchedAt < ttl)
        {
            try
            {
                return JsonSerializer.Deserialize<LeetifyProfile>(hit.PayloadJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Corrupt Leetify cache for {Steam64Id}, refetching", steam64Id);
            }
        }

        LeetifyProfile? profile;
        try
        {
            profile = await client.GetProfileAsync(steam64Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Leetify request failed for {Steam64Id}", steam64Id);
            return null;
        }

        if (profile is null)
        {
            logger.LogWarning("Leetify returned non-success for {Steam64Id}", steam64Id);
            return null;
        }

        var payload = JsonSerializer.Serialize(profile, JsonOptions);
        var now = DateTime.UtcNow;
        var existing = await db.PlayerStatsCache.FirstOrDefaultAsync(c => c.Steam64Id == steam64Id, ct);
        if (existing is null)
            db.PlayerStatsCache.Add(new Domain.PlayerStatsCache { Steam64Id = steam64Id, PayloadJson = payload, FetchedAt = now });
        else
        {
            existing.PayloadJson = payload;
            existing.FetchedAt = now;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent requests missed the cache for the same Steam64Id and both tried
            // to insert (Steam64Id is the primary key). The loser of the race can safely drop
            // its write - both carry the same fresh payload. Surface the error to the caller
            // is pointless here, so just return the profile.
        }

        return profile;
    }
}
