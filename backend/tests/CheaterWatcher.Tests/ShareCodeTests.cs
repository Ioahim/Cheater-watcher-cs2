using CheaterWatcher.Api.Services.Ingestion;
using Xunit;

namespace CheaterWatcher.Tests;

public class ShareCodeTests
{
    [Fact]
    public void Decode_RejectsInvalidLength()
    {
        Assert.False(ShareCode.TryDecode("CSGO-ABC", out _));
        Assert.False(ShareCode.TryDecode("", out _));
    }

    [Fact]
    public void Decode_RejectsAmbiguousCharacters()
    {
        Assert.False(ShareCode.TryDecode("CSGO-IIIIOOOOIIIIIOOOOIIIIIII", out _));
    }

    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var original = new ShareCodeInfo(3836867819375427731UL, 634481683UL, 201);
        var encoded = ShareCode.Encode(original);

        Assert.StartsWith("CSGO-", encoded);
        Assert.Equal(34, encoded.Length);
        Assert.True(ShareCode.TryDecode(encoded, out var decoded));
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Decode_KnownRealWorldVector()
    {
        Assert.True(ShareCode.TryDecode("CSGO-GADqf-jjyJ8-cSP2r-smZRo-TO2xK", out var info));

        Assert.Equal(3230642215713767580UL, info.MatchId);
        Assert.Equal(3230647599455273103UL, info.OutcomeId);
        Assert.Equal((ushort)55788, info.TokenId);
    }

    [Fact]
    public void Encode_KnownRealWorldVector()
    {
        var encoded = ShareCode.Encode(new ShareCodeInfo(3230642215713767580UL, 3230647599455273103UL, 55788));

        Assert.Equal("CSGO-GADqf-jjyJ8-cSP2r-smZRo-TO2xK", encoded);
    }

    [Fact]
    public void Decode_AcceptsCodeWithoutPrefix()
    {
        var encoded = ShareCode.Encode(new ShareCodeInfo(1, 2, 3));
        var bare = encoded["CSGO-".Length..];

        Assert.True(ShareCode.TryDecode(bare, out var decoded));
        Assert.Equal(1UL, decoded.MatchId);
        Assert.Equal(2UL, decoded.OutcomeId);
        Assert.Equal((ushort)3, decoded.TokenId);
    }
}
