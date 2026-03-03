using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class FactionDataService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, FactionData> _unitCache = new();
    private readonly Dictionary<string, FactionExtrasData> _extrasCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly IReadOnlyList<string> FactionNames =
    [
        "beastmen",
        "bretonnia",
        "daemons-of-chaos",
        "dark-elves",
        "dwarfs",
        "high-elves",
        "lizardmen",
        "ogre-kingdoms",
        "orcs-and-goblins",
        "skaven",
        "the-empire",
        "tomb-kings",
        "vampire-counts",
        "warriors-of-chaos",
        "wood-elves"
    ];

    public static readonly IReadOnlyDictionary<string, string> FactionDisplayNames = new Dictionary<string, string>
    {
        ["beastmen"] = "Beastmen",
        ["bretonnia"] = "Bretonnia",
        ["daemons-of-chaos"] = "Daemons of Chaos",
        ["dark-elves"] = "Dark Elves",
        ["dwarfs"] = "Dwarfs",
        ["high-elves"] = "High Elves",
        ["lizardmen"] = "Lizardmen",
        ["ogre-kingdoms"] = "Ogre Kingdoms",
        ["orcs-and-goblins"] = "Orcs & Goblins",
        ["skaven"] = "Skaven",
        ["the-empire"] = "The Empire",
        ["tomb-kings"] = "Tomb Kings",
        ["vampire-counts"] = "Vampire Counts",
        ["warriors-of-chaos"] = "Warriors of Chaos",
        ["wood-elves"] = "Wood Elves"
    };

    public FactionDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task<FactionData> GetFactionUnitsAsync(string factionKey)
    {
        if (_unitCache.TryGetValue(factionKey, out var cached))
            return cached;

        var data = await _http.GetFromJsonAsync<FactionData>(
            $"data/factions/{factionKey}.json", JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load {factionKey}.json");

        NormalizeMountCategories(data);
        _unitCache[factionKey] = data;
        return data;
    }

    private static void NormalizeMountCategories(FactionData data)
    {
        foreach (var unit in data.Units)
        {
            // Already marked as CHARACTER MOUNTS (lizardmen, the-empire) — normalize casing
            if (string.Equals(unit.Category, "CHARACTER MOUNTS", StringComparison.OrdinalIgnoreCase))
            {
                unit.Category = "Character Mounts";
                continue;
            }

            // Detect mount entries: no points cost, no unit size, but has a stat profile
            if (unit.PointsCost == null && unit.UnitSize == null && unit.Profiles.Count > 0)
            {
                unit.Category = "Character Mounts";
            }
        }
    }

    public async Task<FactionExtrasData> GetFactionExtrasAsync(string factionKey)
    {
        if (_extrasCache.TryGetValue(factionKey, out var cached))
            return cached;

        var data = await _http.GetFromJsonAsync<FactionExtrasData>(
            $"data/factions/{factionKey}-extras.json", JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load {factionKey}-extras.json");

        _extrasCache[factionKey] = data;
        return data;
    }

    public static string GetDisplayName(string factionKey)
    {
        return FactionDisplayNames.TryGetValue(factionKey, out var name) ? name : factionKey;
    }
}
