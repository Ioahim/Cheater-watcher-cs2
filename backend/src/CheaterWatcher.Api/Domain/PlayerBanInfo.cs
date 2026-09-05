namespace CheaterWatcher.Api.Domain;

public class PlayerBanInfo
{
    public string Steam64Id { get; set; } = string.Empty;
    public bool CommunityBanned { get; set; }
    public bool VacBanned { get; set; }
    public int NumberOfVACBans { get; set; }
    public int NumberOfGameBans { get; set; }
    public int DaysSinceLastBan { get; set; }
    public string EconomyBan { get; set; } = "none";
    public DateTime FetchedAt { get; set; }
}