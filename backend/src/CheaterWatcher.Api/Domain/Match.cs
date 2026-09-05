namespace CheaterWatcher.Api.Domain;

public enum MatchSource
{
    Upload = 1,
    Replay = 2,
}

public enum ParseStatus
{
    Pending = 1,
    Parsed = 2,
    Failed = 3,
}

public class Match
{
    public Guid Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public string MapName { get; set; } = string.Empty;
    public string Mode { get; set; } = "Unknown";
    public DateTime FinishedAt { get; set; }
    public int CtScore { get; set; }
    public int TScore { get; set; }
    public int? OurTeamNumber { get; set; }
    public MatchSource Source { get; set; }
    public string DemoFileName { get; set; } = string.Empty;
    public string? DemoSourceId { get; set; }
    public ParseStatus Status { get; set; } = ParseStatus.Pending;
    public string? ErrorMessage { get; set; }
    public short? OwnRankType { get; set; }
    public int? OwnRankValue { get; set; }
    public bool Suspected { get; set; }
    public DateTime? ScoredAt { get; set; }
    public DateTime? FlaggedAt { get; set; }
    public DateTime? ParsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool DeleteDemoAfterParse { get; set; }
    public ICollection<MatchPlayer> Players { get; set; } = [];
}
