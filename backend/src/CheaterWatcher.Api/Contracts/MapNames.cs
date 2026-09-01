using System.Globalization;

namespace CheaterWatcher.Api.Contracts;

public static class MapNames
{
    private static readonly Dictionary<string, string> Special = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de_dust2"] = "Dust II",
        ["de_inferno"] = "Inferno",
        ["de_mirage"] = "Mirage",
        ["de_nuke"] = "Nuke",
        ["de_ancient"] = "Ancient",
        ["de_anubis"] = "Anubis",
        ["de_vertigo"] = "Vertigo",
        ["de_train"] = "Train",
        ["de_overpass"] = "Overpass",
        ["de_cache"] = "Cache",
        ["de_mills"] = "Mills",
        ["de_edin"] = "Edin",
        ["de_grail"] = "Grail",
        ["de_brewery"] = "Brewery",
        ["cs_italy"] = "Italy",
        ["cs_office"] = "Office",
        ["cs_agency"] = "Agency",
    };

    public static string Display(string rawMapName)
    {
        if (Special.TryGetValue(rawMapName, out var display))
            return display;

        var stripped = rawMapName.Contains('_')
            ? rawMapName[(rawMapName.IndexOf('_') + 1)..]
            : rawMapName;
        var words = stripped.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(" ", words.Select(w =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(w.ToLowerInvariant())));
        return joined.Length > 0 ? char.ToUpperInvariant(joined[0]) + joined[1..] : rawMapName;
    }
}
