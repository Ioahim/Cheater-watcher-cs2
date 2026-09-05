using System.Collections.Concurrent;
using System.Text.Json;
using CheaterWatcher.Api.Data;
using CheaterWatcher.Api.Domain;
using CheaterWatcher.Api.Services.Leetify;
using CheaterWatcher.Api.Services.Suspicion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services;

public class MatchSuspicionService(IServiceScopeFactory scopeFactory, ISuspicionScorer scorer, IOptions<SuspicionOptions> options)
{
    public async Task ScoreMatchAsync(AppDbContext db, Match match, CancellationToken ct)
    {
        var opt = options.Value;
        var playerRows = await db.MatchPlayers.Where(p => p.MatchId == match.Id).ToListAsync(ct);
        var groups = playerRows.GroupBy(p => p.Steam64Id).ToList();

        var profiles = new ConcurrentDictionary<string, LeetifyProfile?>();
        await Task.WhenAll(groups.Select(async group =>
        {
            using var scope = scopeFactory.CreateScope();
            var leetify = scope.ServiceProvider.GetRequiredService<LeetifyService>();
            profiles[group.Key] = await leetify.GetProfileAsync(group.Key, ct);
        }));

        var anySuspected = false;
        foreach (var group in groups)
        {
            var profile = profiles[group.Key];
            SuspicionResult result;
            if (profile is null || !profile.IsPublic || profile.Stats is null)
            {
                result = new SuspicionResult(null, IsKnown: false, opt.Threshold, []);
            }
            else
            {
                result = scorer.Score(SuspicionInput.From(profile));
            }

            var breakdownJson = JsonSerializer.Serialize(result.Rules);
            foreach (var row in group)
            {
                row.SuspicionScore = result.Score;
                row.SuspicionBreakdownJson = breakdownJson;
            }

            if (result.Suspected)
                anySuspected = true;
        }

        match.Suspected = anySuspected;
        match.ScoredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}