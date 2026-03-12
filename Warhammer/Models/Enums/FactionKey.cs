namespace Warhammer.Models.Enums;

/// <summary>
/// Identifies a faction. Maps to the JSON filename and FactionRaces keys.
/// </summary>
public enum FactionKey
{
    Beastmen,
    Bretonnia,
    DaemonsOfChaos,
    DarkElves,
    Dwarfs,
    HighElves,
    Lizardmen,
    OgreKingdoms,
    OrcsAndGoblins,
    Skaven,
    TheEmpire,
    TombKings,
    VampireCounts,
    WarriorsOfChaos,
    WoodElves,
}

public static class FactionKeyExtensions
{
    private static readonly Dictionary<string, FactionKey> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["beastmen"] = FactionKey.Beastmen,
        ["bretonnia"] = FactionKey.Bretonnia,
        ["daemons-of-chaos"] = FactionKey.DaemonsOfChaos,
        ["dark-elves"] = FactionKey.DarkElves,
        ["dwarfs"] = FactionKey.Dwarfs,
        ["high-elves"] = FactionKey.HighElves,
        ["lizardmen"] = FactionKey.Lizardmen,
        ["ogre-kingdoms"] = FactionKey.OgreKingdoms,
        ["orcs-and-goblins"] = FactionKey.OrcsAndGoblins,
        ["skaven"] = FactionKey.Skaven,
        ["the-empire"] = FactionKey.TheEmpire,
        ["tomb-kings"] = FactionKey.TombKings,
        ["vampire-counts"] = FactionKey.VampireCounts,
        ["warriors-of-chaos"] = FactionKey.WarriorsOfChaos,
        ["wood-elves"] = FactionKey.WoodElves,
    };

    private static readonly Dictionary<FactionKey, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly Dictionary<FactionKey, string> DisplayNames = new()
    {
        [FactionKey.Beastmen] = "Beastmen",
        [FactionKey.Bretonnia] = "Bretonnia",
        [FactionKey.DaemonsOfChaos] = "Daemons of Chaos",
        [FactionKey.DarkElves] = "Dark Elves",
        [FactionKey.Dwarfs] = "Dwarfs",
        [FactionKey.HighElves] = "High Elves",
        [FactionKey.Lizardmen] = "Lizardmen",
        [FactionKey.OgreKingdoms] = "Ogre Kingdoms",
        [FactionKey.OrcsAndGoblins] = "Orcs & Goblins",
        [FactionKey.Skaven] = "Skaven",
        [FactionKey.TheEmpire] = "The Empire",
        [FactionKey.TombKings] = "Tomb Kings",
        [FactionKey.VampireCounts] = "Vampire Counts",
        [FactionKey.WarriorsOfChaos] = "Warriors of Chaos",
        [FactionKey.WoodElves] = "Wood Elves",
    };

    public static string ToJsonKey(this FactionKey key) => ToJson[key];

    public static string ToDisplayName(this FactionKey key) => DisplayNames[key];

    public static bool TryFromJsonKey(string jsonKey, out FactionKey result)
        => FromJson.TryGetValue(jsonKey, out result);

    public static IReadOnlyList<FactionKey> All { get; } = Enum.GetValues<FactionKey>();

    public static bool IsElf(this FactionKey key) =>
        key is FactionKey.DarkElves or FactionKey.HighElves or FactionKey.WoodElves;
}
