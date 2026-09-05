using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheaterWatcher.Api.Services;

public static class ReplaySettingsStore
{
    public static async Task<ReplayScanSettings> GetOrCreateAsync(AppDbContext db, CancellationToken ct)
    {
        var settings = await db.ReplayScanSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            // Start unconfigured so the app's first-run prompt shows. The user's path
            // is persisted only when they save it in the app (which also writes .env).
            settings = new ReplayScanSettings();
            db.ReplayScanSettings.Add(settings);
            await db.SaveChangesAsync(ct);
        }
        return settings;
    }
}