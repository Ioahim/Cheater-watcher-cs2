using DemoFile;

namespace CheaterWatcher.Api.Services;

public sealed record ExtractedPlayer(
    string Steam64Id,
    string Name,
    int TeamNumber,
    int Kills,
    int Deaths,
    int Assists,
    short? RankType,
    int? RankValue);

public sealed record ExtractedDemo(
    string MapName,
    string Mode,
    int CtScore,
    int TScore,
    IReadOnlyList<ExtractedPlayer> Players);

public class DemoExtractor
{
    public async Task<ExtractedDemo> ExtractAsync(string demoPath, CancellationToken ct = default)
    {
        var demo = new CsDemoParser();
        var stats = new Dictionary<ulong, PlayerAccumulator>();

        demo.Source1GameEvents.PlayerDeath += e =>
        {
            var victim = e.Player;
            if (victim is { SteamID: > 0 })
                Acc(stats, victim.SteamID).Deaths++;

            var attacker = e.Attacker;
            if (attacker is { SteamID: > 0 } && !ReferenceEquals(attacker, victim))
                Acc(stats, attacker.SteamID).Kills++;

            var assister = e.Assister;
            if (assister is { SteamID: > 0 }
                && !ReferenceEquals(assister, victim)
                && !ReferenceEquals(assister, attacker))
                Acc(stats, assister.SteamID).Assists++;
        };

        await using var stream = File.OpenRead(demoPath);
        var reader = DemoFileReader.Create(demo, stream);
        await reader.ReadAllAsync(ct);

        var players = demo.PlayersIncludingDisconnected
            .Where(p =>
            {
                if (p.SteamID == 0 || p.PlayerName == "DemoRecorder")
                    return false;
                if (p.PlayerInfo is { } info && (info.Ishltv || info.Fakeplayer))
                    return false;
                return true;
            })
            .Select(p =>
            {
                stats.TryGetValue(p.SteamID, out var acc);
                return new ExtractedPlayer(
                    p.SteamID.ToString(),
                    p.PlayerName,
                    p.TeamNum,
                    acc?.Kills ?? 0,
                    acc?.Deaths ?? 0,
                    acc?.Assists ?? 0,
                    p.CompetitiveRanking > 0 ? p.CompetitiveRankType : null,
                    p.CompetitiveRanking > 0 ? p.CompetitiveRanking : null);
            })
            .OrderByDescending(p => p.Kills)
            .ToList();

        return new ExtractedDemo(
            demo.FileHeader?.MapName ?? "unknown",
            MapMode(demo.GameRules.QueuedMatchmakingMode),
            demo.TeamCounterTerrorist.Score,
            demo.TeamTerrorist.Score,
            players);
    }

    private static PlayerAccumulator Acc(Dictionary<ulong, PlayerAccumulator> stats, ulong steamId)
    {
        if (!stats.TryGetValue(steamId, out var acc))
        {
            acc = new PlayerAccumulator();
            stats[steamId] = acc;
        }
        return acc;
    }

    private static string MapMode(int queuedMode) => queuedMode switch
    {
        1 => "Premier",
        2 => "Competitive",
        3 => "Wingman",
        0 => "Unknown",
        _ => $"Queue{queuedMode}",
    };

    private sealed class PlayerAccumulator
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
    }
}
