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
    bool SteamLinked)
{
    public static AccountDto From(Account a) => new(
        a.Id,
        a.Name,
        a.AvatarUrl,
        a.PremierRating,
        a.WingmanLevel,
        [.. a.MapRanks.Select(r => new MapRankDto(r.Map, r.Level)).OrderByDescending(r => r.Level)],
        !string.IsNullOrWhiteSpace(a.Steam64Id));
}

public sealed record MatchDto(
    string Id,
    string Result,
    string Score,
    string Map,
    string Mode,
    RankDto? Rank,
    string? Date,
    bool Suspected,
    bool Flagged,
    string Status,
    string? ScoredAt,
    bool HasFlaggedPlayer);

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
    RankDto? Rank,
    bool VacBanned,
    bool IsOwnAccount);

public sealed record MatchRosterDto(IReadOnlyList<MatchPlayerDto> Ct, IReadOnlyList<MatchPlayerDto> T, RankDto? AverageRank);

public sealed record UploadResponse(Guid MatchId, bool Duplicate);

public sealed record MatchStatusDto(Guid Id, string Status, string? Error, bool Suspected, bool Flagged);

public sealed record FlagPlayerRequest(int Reason = 1, string? Note = null);

public sealed record AccountStatsDto(
    int TotalMatches,
    int FlaggedMatches,
    int FlaggedPlayers,
    int BannedPlayers,
    double WinRate,
    int TotalPlayers,
    IReadOnlyList<MapStatDto> ByMap,
    IReadOnlyList<ModeStatDto> ByMode,
    IReadOnlyList<FlaggedPlayerDto> FlaggedPlayersList);

public sealed record FlaggedPlayerDto(
    string Steam64Id,
    string Name,
    int FlagReason,
    string? FlagNote,
    bool VacBanned,
    int Encounters);

public sealed record MapStatDto(string Map, int Matches, double WinRate);

public sealed record ModeStatDto(string Mode, int Matches);

public sealed record PlayerEncounterDto(
    Guid MatchId,
    string Map,
    string Mode,
    string? Date,
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
    bool VacBanned,
    IReadOnlyList<PlayerEncounterDto> Encounters);

public sealed record SteamExchangeRequest(string Code);

public sealed record ReorderRequest(List<int> Order);

public sealed record ReplaySettingsDto(
    bool HasPath,
    string HostPath,
    string EffectivePath,
    int ScanIntervalMinutes,
    bool RestartRequired,
    DateTime? LastScanAt,
    int LastScanNew,
    int LastScanAttributed,
    int LastScanPending,
    string? LastScanError);

public sealed record UpdateReplaySettingsRequest(string? Path);

public sealed record SaveReplayPathResult(bool Saved, bool RestartRequired, bool CanWriteEnv, string HostPath);

public sealed record SteamKeyStatusDto(
    bool Configured,
    bool Active,
    string? KeyHint,
    bool RestartRequired,
    bool CanWriteEnv);

public sealed record UpdateSteamKeyRequest(string? Key);

public sealed record SaveSteamKeyResult(
    bool Saved,
    bool Valid,
    bool Checked,
    bool RestartRequired,
    bool CanWriteEnv);

public sealed record PendingReplayPlayerDto(string Steam64Id, string Name, bool Linked);

public sealed record PendingReplayDto(
    Guid Id,
    string FileName,
    string MapName,
    string Mode,
    DateTime DiscoveredAt,
    IReadOnlyList<PendingReplayPlayerDto> Players,
    IReadOnlyList<int> LinkedAccountOptions);

public sealed record ResolvePendingReplayRequest(int? AccountId, bool? Dismiss);
