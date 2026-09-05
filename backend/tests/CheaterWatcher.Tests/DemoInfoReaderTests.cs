using CheaterWatcher.Api.Services.Ingestion;
using Xunit;

namespace CheaterWatcher.Tests;

public class DemoInfoReaderTests
{
    private const string FixtureInfoPath =
        @"..\..\..\Fixtures\Demos\match730_003836867819375427731_0634481683_201.dem.info";

    [Fact]
    public void TryReadStartTime_RealInfoFixture_ReturnsMatchStartTime()
    {
        var reader = new DemoInfoReader();
        var demoPath = FixtureInfoPath[..^5]; // strip ".info"

        var startTime = reader.TryReadStartTime(demoPath);

        Assert.NotNull(startTime);
        Assert.Equal(new DateTime(2026, 8, 14, 3, 44, 8, DateTimeKind.Utc), startTime);
    }

    [Fact]
    public void TryReadStartTime_MissingInfo_ReturnsNull()
    {
        var reader = new DemoInfoReader();

        var startTime = reader.TryReadStartTime("C:/definitely/missing.dem");

        Assert.Null(startTime);
    }

    [Fact]
    public void TryReadStartTime_CorruptInfo_ReturnsNull()
    {
        var reader = new DemoInfoReader();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
            var startTime = reader.TryReadStartTime(path);
            Assert.Null(startTime);
        }
        finally
        {
            File.Delete(path);
        }
    }
}