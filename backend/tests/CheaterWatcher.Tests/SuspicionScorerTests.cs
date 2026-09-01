using CheaterWatcher.Api.Services.Suspicion;
using Xunit;

namespace CheaterWatcher.Tests;

public class SuspicionScorerTests
{
    private static SuspicionOptions Options() => new()
    {
        Threshold = 70,
        PreaimBelowDeg = 7,
        PreaimWeight = 25,
        ReactionTimeBelowMs = 250,
        ReactionTimeWeight = 20,
        HeadshotAccuracyAbovePct = 45,
        HeadshotAccuracyWeight = 25,
        SprayAccuracyAbovePct = 55,
        SprayAccuracyWeight = 15,
        CounterStrafingAbovePct = 96,
        CounterStrafingWeight = 10,
        PlatformBanWeight = 20,
    };

    [Fact]
    public void Score_CleanPlayer_IsBelowThreshold()
    {
        var scorer = new RuleBasedSuspicionScorer(Microsoft.Extensions.Options.Options.Create(Options()));
        var input = new SuspicionInput(
            PreaimDeg: 12.5,
            ReactionTimeMs: 586,
            HeadshotAccuracyPct: 18,
            SprayAccuracyPct: 37,
            CounterStrafingPct: 80.7,
            HasPlatformBan: false);

        var result = scorer.Score(input);

        Assert.True(result.IsKnown);
        Assert.Equal(0, result.Score);
        Assert.False(result.Suspected);
        Assert.All(result.Rules, r => Assert.False(r.Triggered));
    }

    [Fact]
    public void Score_ExtremeStats_TriggersAllRulesAndSuspects()
    {
        var scorer = new RuleBasedSuspicionScorer(Microsoft.Extensions.Options.Options.Create(Options()));
        var input = new SuspicionInput(5, 200, 60, 70, 99, true);

        var result = scorer.Score(input);

        Assert.Equal(100, result.Score);
        Assert.Equal(6, result.Rules.Count(r => r.Triggered));
        Assert.True(result.Suspected);
    }

    [Fact]
    public void Score_UsesStrictComparisons()
    {
        var scorer = new RuleBasedSuspicionScorer(Microsoft.Extensions.Options.Options.Create(Options()));

        var atThreshold = scorer.Score(new SuspicionInput(7, 250, 45, 55, 96, false));
        Assert.Equal(0, atThreshold.Score);
        Assert.False(atThreshold.Suspected);

        var pastThreshold = scorer.Score(new SuspicionInput(6.9, 249.9, 45.1, 55.1, 96.1, false));
        Assert.Equal(95, pastThreshold.Score);
        Assert.True(pastThreshold.Suspected);
    }

    [Fact]
    public void Score_UnknownProfile_HasNullScoreAndIsNotSuspected()
    {
        var unknown = new SuspicionResult(null, IsKnown: false, Threshold: 70, Array.Empty<RuleHit>());

        Assert.Null(unknown.Score);
        Assert.False(unknown.IsKnown);
        Assert.False(unknown.Suspected);
    }
}
