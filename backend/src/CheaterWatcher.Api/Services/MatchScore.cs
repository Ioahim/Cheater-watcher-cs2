namespace CheaterWatcher.Api.Services;

public static class MatchScore
{
    public static (int Our, int Their) SplitScores(int ctScore, int tScore, int? ourTeam) => ourTeam switch
    {
        3 => (ctScore, tScore),
        2 => (tScore, ctScore),
        _ => (ctScore, tScore),
    };

    public static string ResultChar(int? ourTeam, int our, int their) => ourTeam is null
        ? "D"
        : our > their ? "W" : our < their ? "L" : "D";
}
