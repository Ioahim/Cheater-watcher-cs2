using CheaterWatcher.Api.Domain;

namespace CheaterWatcher.Api.Contracts;

public sealed record MapRankDto(string Map, int Level);

public sealed record RankDto(string Kind, int? Rating, int? Level)
{
    public static RankDto? FromCapture(string mode, short? rankType, int? rankValue)
    {
        if (rankType is null || rankValue is not { } value || value <= 0)
            return null;

        if (rankType == CsRankTypes.Premier)
            return mode == "Premier" ? new RankDto("premier", value, null) : null;

        if (value is >= 1 and <= 18)
            return mode switch
            {
                "Competitive" => new RankDto("competitive", null, value),
                "Wingman" => new RankDto("wingman", null, value),
                _ => null,
            };

        return null;
    }
}

public sealed record AccountDto(
    int Id,
    string Name,
    string? AvatarUrl,
    int? PremierRating,
    int? WingmanLevel,
    IReadOnlyList<MapRankDto> CompetitiveRanks,
    bool SteamLinked,
    bool TrackingEnabled,
    bool NeedsShareCode)
{
    public static AccountDto From(Account a) => new(
        a.Id,
        a.Name,
        a.User?.AvatarUrl,
        a.PremierRating,
        a.WingmanLevel,
        [.. a.MapRanks.Select(r => new MapRankDto(r.Map, r.Level)).OrderByDescending(r => r.Level)],
        !string.IsNullOrWhiteSpace(a.Steam64Id),
        !string.IsNullOrWhiteSpace(a.Steam64Id) && !string.IsNullOrWhiteSpace(a.AuthCode),
        !string.IsNullOrWhiteSpace(a.Steam64Id) && string.IsNullOrWhiteSpace(a.LatestShareCode));
}

public sealed record MatchDto(
    string Id,
    string Result,
    string Score,
    string Map,
    string Mode,
    RankDto? Rank,
    string Date,
    bool Suspected,
    bool Flagged,
    string Status);

public sealed record PlayerReasonDto(string Name, string Detail);

public sealed record MatchPlayerDto(
    long Id,
    string Name,
    string Steam64Id,
    int Kills,
    int Deaths,
    int Assists,
    bool Suspected,
    IReadOnlyList<PlayerReasonDto> Reasons,
    bool Flagged,
    int FlagReason,
    string? FlagNote,
    RankDto? Rank);

public sealed record MatchRosterDto(IReadOnlyList<MatchPlayerDto> Ct, IReadOnlyList<MatchPlayerDto> T);

public sealed record UploadResponse(Guid MatchId, bool Duplicate);

public sealed record MatchStatusDto(Guid Id, string Status, string? Error, bool Suspected, bool Flagged);

public sealed record FlagPlayerRequest(int Reason = 1, string? Note = null);

public sealed record AccountStatsDto(
    int TotalMatches,
    int FlaggedMatches,
    int FlaggedPlayers,
    double WinRate,
    int TotalPlayers,
    IReadOnlyList<MapStatDto> ByMap,
    IReadOnlyList<ModeStatDto> ByMode);

public sealed record MapStatDto(string Map, int Matches, double WinRate);

public sealed record ModeStatDto(string Mode, int Matches);

public sealed record PlayerEncounterDto(
    Guid MatchId,
    string Map,
    string Mode,
    string Date,
    string Result,
    int Kills,
    int Deaths,
    int Assists,
    int TeamNumber,
    int FlagReason,
    string? FlagNote);

public sealed record PlayerDetailDto(
    string Steam64Id,
    string Name,
    int TimesEncountered,
    int TimesOnOurTeam,
    int TimesAgainstUs,
    int TotalKills,
    int TotalDeaths,
    int TotalAssists,
    bool Flagged,
    int FlagReason,
    string? FlagNote,
    IReadOnlyList<PlayerEncounterDto> Encounters);

public sealed record UpdateCredentialsRequest(string? Steam64Id, string? AuthCode, string? ShareCode = null);

public sealed record AddShareCodeRequest(int AccountId, string ShareCode);

public sealed record AddShareCodeResponse(string Status, Guid? MatchId = null);

public sealed record AuthUserDto(int Id, string Username, string? Steam64Id, string? AvatarUrl, int? OwnAccountId);

public sealed record RegisterRequest(string Username, string Password);

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(string Token, AuthUserDto User);

public sealed record SteamExchangeRequest(string Code);
