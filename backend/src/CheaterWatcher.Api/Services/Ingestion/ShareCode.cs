using System.Buffers.Binary;
using System.Numerics;

namespace CheaterWatcher.Api.Services.Ingestion;

public sealed record ShareCodeInfo(ulong MatchId, ulong OutcomeId, ushort TokenId);

public static class ShareCode
{
    public const string Prefix = "CSGO-";
    private const string Dictionary = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";
    private const int EncodedLength = 25;

    public static bool TryDecode(string code, out ShareCodeInfo info)
    {
        info = new ShareCodeInfo(0, 0, 0);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var clean = code.Replace("CSGO", "").Replace("-", "");
        if (clean.Length != EncodedLength)
            return false;

        var number = BigInteger.Zero;
        foreach (var c in clean.Reverse())
        {
            var idx = Dictionary.IndexOf(c);
            if (idx < 0)
                return false;
            number = number * Dictionary.Length + idx;
        }

        var littleEndian = number.ToByteArray();
        if (littleEndian.Length > 19)
            return false;

        Span<byte> buffer = stackalloc byte[18];
        buffer.Clear();
        littleEndian.AsSpan(0, Math.Min(littleEndian.Length, 18)).CopyTo(buffer);
        for (var i = 18; i < littleEndian.Length; i++)
        {
            if (littleEndian[i] != 0)
                return false;
        }

        info = new ShareCodeInfo(
            BinaryPrimitives.ReadUInt64BigEndian(buffer[10..]),
            BinaryPrimitives.ReadUInt64BigEndian(buffer[2..10]),
            BinaryPrimitives.ReadUInt16BigEndian(buffer[..2]));
        return true;
    }

    public static string Encode(ShareCodeInfo info)
    {
        Span<byte> buffer = stackalloc byte[18];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, info.TokenId);
        BinaryPrimitives.WriteUInt64BigEndian(buffer[2..], info.OutcomeId);
        BinaryPrimitives.WriteUInt64BigEndian(buffer[10..], info.MatchId);

        var bytes = buffer.ToArray();
        if ((bytes[17] & 0x80) != 0)
            bytes = [..bytes, (byte)0];

        var number = new BigInteger(bytes);
        Span<char> chars = stackalloc char[EncodedLength];
        for (var i = 0; i < EncodedLength; i++)
        {
            number = BigInteger.DivRem(number, Dictionary.Length, out var remainder);
            chars[i] = Dictionary[(int)remainder];
        }

        return $"{Prefix}{chars[0..5]}-{chars[5..10]}-{chars[10..15]}-{chars[15..20]}-{chars[20..25]}";
    }
}
