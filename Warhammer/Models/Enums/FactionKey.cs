namespace Warhammer.Models.Enums;

/// <summary>
/// Identifies a faction. Maps to the JSON filename and FactionRaces keys.
/// </summary>
public enum FactionKey
{
    Albion,
    Amazons,
    Araby,
    Beastmen,
    Bretonnia,
    ChaosDwarfs,
    DaemonsOfChaos,
    DarkElves,
    DogsOfWar,
    Dwarfs,
    Estalia,
    GrandCathay,
    Halflings,
    HighElves,
    Hobgoblins,
    KingdomsOfInd,
    Kislev,
    Lizardmen,
    Nippon,
    Norsca,
    OgreKingdoms,
    OrcsAndGoblins,
    PiratesOfSartosa,
    Skaven,
    TheEmpire,
    TombKings,
    VampireCounts,
    WarriorsOfChaos,
    WoodElves,
    ZombiePirates,
}

public static class FactionKeyExtensions
{
    private static readonly Dictionary<string, FactionKey> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["albion"] = FactionKey.Albion,
        ["amazons"] = FactionKey.Amazons,
        ["araby"] = FactionKey.Araby,
        ["beastmen"] = FactionKey.Beastmen,
        ["bretonnia"] = FactionKey.Bretonnia,
        ["chaos-dwarfs"] = FactionKey.ChaosDwarfs,
        ["daemons-of-chaos"] = FactionKey.DaemonsOfChaos,
        ["dark-elves"] = FactionKey.DarkElves,
        ["dogs-of-war"] = FactionKey.DogsOfWar,
        ["dwarfs"] = FactionKey.Dwarfs,
        ["estalia"] = FactionKey.Estalia,
        ["grand-cathay"] = FactionKey.GrandCathay,
        ["halflings"] = FactionKey.Halflings,
        ["high-elves"] = FactionKey.HighElves,
        ["hobgoblins"] = FactionKey.Hobgoblins,
        ["kingdoms-of-ind"] = FactionKey.KingdomsOfInd,
        ["kislev"] = FactionKey.Kislev,
        ["lizardmen"] = FactionKey.Lizardmen,
        ["nippon"] = FactionKey.Nippon,
        ["norsca"] = FactionKey.Norsca,
        ["ogre-kingdoms"] = FactionKey.OgreKingdoms,
        ["orcs-and-goblins"] = FactionKey.OrcsAndGoblins,
        ["pirates-of-sartosa"] = FactionKey.PiratesOfSartosa,
        ["skaven"] = FactionKey.Skaven,
        ["the-empire"] = FactionKey.TheEmpire,
        ["tomb-kings"] = FactionKey.TombKings,
        ["vampire-counts"] = FactionKey.VampireCounts,
        ["warriors-of-chaos"] = FactionKey.WarriorsOfChaos,
        ["wood-elves"] = FactionKey.WoodElves,
        ["zombie-pirates"] = FactionKey.ZombiePirates,
    };

    private static readonly Dictionary<FactionKey, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly Dictionary<FactionKey, string> DisplayNames = new()
    {
        [FactionKey.Albion] = "Albion",
        [FactionKey.Amazons] = "Amazons",
        [FactionKey.Araby] = "Araby",
        [FactionKey.Beastmen] = "Beastmen",
        [FactionKey.Bretonnia] = "Bretonnia",
        [FactionKey.ChaosDwarfs] = "Chaos Dwarfs",
        [FactionKey.DaemonsOfChaos] = "Daemons of Chaos",
        [FactionKey.DarkElves] = "Dark Elves",
        [FactionKey.DogsOfWar] = "Dogs of War",
        [FactionKey.Dwarfs] = "Dwarfs",
        [FactionKey.Estalia] = "Estalia",
        [FactionKey.GrandCathay] = "Grand Cathay",
        [FactionKey.Halflings] = "Halflings",
        [FactionKey.HighElves] = "High Elves",
        [FactionKey.Hobgoblins] = "Hobgoblins",
        [FactionKey.KingdomsOfInd] = "Kingdoms of Ind",
        [FactionKey.Kislev] = "Kislev",
        [FactionKey.Lizardmen] = "Lizardmen",
        [FactionKey.Nippon] = "Nippon",
        [FactionKey.Norsca] = "Norsca",
        [FactionKey.OgreKingdoms] = "Ogre Kingdoms",
        [FactionKey.OrcsAndGoblins] = "Orcs & Goblins",
        [FactionKey.PiratesOfSartosa] = "Pirates of Sartosa",
        [FactionKey.Skaven] = "Skaven",
        [FactionKey.TheEmpire] = "The Empire",
        [FactionKey.TombKings] = "Tomb Kings",
        [FactionKey.VampireCounts] = "Vampire Counts",
        [FactionKey.WarriorsOfChaos] = "Warriors of Chaos",
        [FactionKey.WoodElves] = "Wood Elves",
        [FactionKey.ZombiePirates] = "Zombie Pirates",
    };

    public static string ToJsonKey(this FactionKey key) => ToJson[key];

    public static string ToDisplayName(this FactionKey key) => DisplayNames[key];

    public static bool TryFromJsonKey(string jsonKey, out FactionKey result)
        => FromJson.TryGetValue(jsonKey, out result);

    /// <summary>The display name for a JSON key; an unknown key is shown as it is, a missing one as a dash.</summary>
    public static string DisplayNameForJsonKey(string? jsonKey) =>
        jsonKey is null ? "—" : TryFromJsonKey(jsonKey, out var key) ? key.ToDisplayName() : jsonKey;

    public static IReadOnlyList<FactionKey> All { get; } = Enum.GetValues<FactionKey>();

    public static bool IsElf(this FactionKey key) =>
        key is FactionKey.DarkElves or FactionKey.HighElves or FactionKey.WoodElves;
}
