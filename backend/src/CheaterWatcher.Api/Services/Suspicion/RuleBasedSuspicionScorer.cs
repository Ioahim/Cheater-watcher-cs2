using System.Globalization;
using Microsoft.Extensions.Options;

namespace CheaterWatcher.Api.Services.Suspicion;

public class SuspicionOptions
{
    public int Threshold { get; set; } = 70;
    public double PreaimBelowDeg { get; set; } = 7;
    public int PreaimWeight { get; set; } = 25;
    public double ReactionTimeBelowMs { get; set; } = 250;
    public int ReactionTimeWeight { get; set; } = 20;
    public double HeadshotAccuracyAbovePct { get; set; } = 45;
    public int HeadshotAccuracyWeight { get; set; } = 25;
    public double SprayAccuracyAbovePct { get; set; } = 55;
    public int SprayAccuracyWeight { get; set; } = 15;
    public double CounterStrafingAbovePct { get; set; } = 96;
    public int CounterStrafingWeight { get; set; } = 10;
    public int PlatformBanWeight { get; set; } = 20;
}

public sealed record SuspicionInput(
    double? PreaimDeg,
    double? ReactionTimeMs,
    double? HeadshotAccuracyPct,
    double? SprayAccuracyPct,
    double? CounterStrafingPct,
    bool HasPlatformBan);

public sealed record RuleHit(string Name, string Detail, int Weight, bool Triggered);

public sealed record SuspicionResult(double? Score, bool IsKnown, int Threshold, IReadOnlyList<RuleHit> Rules)
{
    public bool Suspected => Score is { } s && s >= Threshold;
}

public interface ISuspicionScorer
{
    SuspicionResult Score(SuspicionInput input);
}

public class RuleBasedSuspicionScorer : ISuspicionScorer
{
    private readonly SuspicionOptions _options;

    public RuleBasedSuspicionScorer(IOptions<SuspicionOptions> options)
    {
        _options = options.Value;
    }

    public SuspicionResult Score(SuspicionInput input)
    {
        var rules = new List<RuleHit>
        {
            Compare("Preaim", input.PreaimDeg, _options.PreaimBelowDeg, "°", below: true, _options.PreaimWeight),
            Compare("Reaction time", input.ReactionTimeMs, _options.ReactionTimeBelowMs, "ms", below: true, _options.ReactionTimeWeight),
            Compare("Headshot accuracy", input.HeadshotAccuracyPct, _options.HeadshotAccuracyAbovePct, "%", below: false, _options.HeadshotAccuracyWeight),
            Compare("Spray accuracy", input.SprayAccuracyPct, _options.SprayAccuracyAbovePct, "%", below: false, _options.SprayAccuracyWeight),
            Compare("Counter-strafing", input.CounterStrafingPct, _options.CounterStrafingAbovePct, "%", below: false, _options.CounterStrafingWeight),
            new("Platform bans", input.HasPlatformBan ? "Player has platform bans" : "No known platform bans", _options.PlatformBanWeight, input.HasPlatformBan),
        };

        var score = Math.Min(100, rules.Where(r => r.Triggered).Sum(r => r.Weight));
        return new SuspicionResult(score, IsKnown: true, _options.Threshold, rules);
    }

    private static RuleHit Compare(string name, double? value, double threshold, string unit, bool below, int weight)
    {
        if (value is null)
            return new RuleHit(name, "Stat unavailable", weight, Triggered: false);

        var triggered = below ? value < threshold : value > threshold;
        var detail = string.Create(CultureInfo.InvariantCulture, $"{value:F1}{unit} vs {(below ? "<" : ">")} {threshold}{unit}");
        return new RuleHit(name, detail, weight, triggered);
    }
}
