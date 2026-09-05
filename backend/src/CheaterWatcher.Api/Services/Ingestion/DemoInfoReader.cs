namespace CheaterWatcher.Api.Services.Ingestion;

/// <summary>
/// Reads the CS2 <c>.dem.info</c> sidecar (CMsgGCCStrike15_v2_MatchInfo) written
/// next to each downloaded replay. Only the minimal top-level fields we need are
/// decoded: field 2 is the match <c>starttime</c> (Unix seconds) - the demo file
/// itself carries no wall-clock timestamp.
/// </summary>
public class DemoInfoReader
{
    /// <summary>Returns the match start time in UTC, or null when the .info is
    /// missing, unreadable, or does not contain a usable starttime.</summary>
    public DateTime? TryReadStartTime(string demoPath)
    {
        var infoPath = demoPath + ".info";
        if (!File.Exists(infoPath))
            return null;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(infoPath);
        }
        catch
        {
            return null;
        }

        try
        {
            return ReadStartTime(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? ReadStartTime(byte[] bytes)
    {
        var i = 0;
        while (i + 1 < bytes.Length)
        {
            if (!TryReadVarint(bytes, ref i, out var key))
                break;

            var field = (int)(key >> 3);
            var wireType = (int)(key & 7);

            if (field == 2 && wireType == 0)
            {
                if (!TryReadVarint(bytes, ref i, out var value))
                    return null;
                if (value > 0 && value < 4_000_000_000L)
                    return DateTimeOffset.FromUnixTimeSeconds((long)value).UtcDateTime;
                return null;
            }

            switch (wireType)
            {
                case 0:
                    if (!TryReadVarint(bytes, ref i, out _))
                        return null;
                    break;
                case 1:
                    i += 8;
                    break;
                case 2:
                    if (!TryReadVarint(bytes, ref i, out var len))
                        return null;
                    i += (int)len;
                    break;
                case 5:
                    i += 4;
                    break;
                default:
                    return null;
            }
        }

        return null;
    }

    private static bool TryReadVarint(byte[] bytes, ref int i, out ulong value)
    {
        value = 0;
        var shift = 0;
        while (i < bytes.Length)
        {
            var b = bytes[i++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return true;
            shift += 7;
            if (shift >= 64)
                return false;
        }
        return false;
    }
}
