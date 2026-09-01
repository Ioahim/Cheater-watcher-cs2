using CheaterWatcher.Api.Services;
using Xunit;

namespace CheaterWatcher.Tests;

public class DemoExtractorTests
{
    private const string FixturePath =
        @"..\..\..\Fixtures\Demos\match730_003836867819375427731_0634481683_201.dem";

    [Fact]
    public async Task Extract_ParsesRealDemoFixture()
    {
        if (!File.Exists(FixturePath))
            return;

        var extractor = new DemoExtractor();
        var demo = await extractor.ExtractAsync(FixturePath, CancellationToken.None);

        Assert.Equal("de_mirage", demo.MapName);
        Assert.Equal(13, Math.Max(demo.CtScore, demo.TScore));
        Assert.Equal(5, Math.Min(demo.CtScore, demo.TScore));
        Assert.Equal(10, demo.Players.Count);
        Assert.All(demo.Players, p => Assert.NotEqual("0", p.Steam64Id));
        Assert.Contains(demo.Players, p => p.Kills > 0);
    }
}
