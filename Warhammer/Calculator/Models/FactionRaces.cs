namespace Warhammer.Calculator.Models;

/// <summary>
/// Maps faction ids — the bundle's slugs — to race keywords used for conditional rules
/// like Hatred (target) and Fear Elves.
/// </summary>
public static class FactionRaces
{
    private static readonly Dictionary<string, HashSet<string>> RaceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["albion"] = ["Albion", "Human", "Humans"],
        ["amazons"] = ["Amazons", "Amazon", "Human", "Humans"],
        ["araby"] = ["Araby", "Arabyans", "Human", "Humans"],
        ["beastmen"] = ["Beastmen", "Beastman"],
        ["bretonnia"] = ["Bretonnia", "Bretonnians", "Human", "Humans"],
        ["chaos-dwarfs"] = ["Chaos Dwarfs", "Chaos Dwarf", "Dwarfs", "Dwarf", "Dwarves", "Chaos"],
        ["daemons-of-chaos"] = ["Daemons", "Daemon", "Daemons of Chaos", "Chaos"],
        ["dark-elves"] = ["Dark Elves", "Dark Elf", "Elves", "Elf"],
        ["dogs-of-war"] = ["Dogs of War", "Mercenaries", "Human", "Humans"],
        ["dwarfs"] = ["Dwarfs", "Dwarf", "Dwarves"],
        ["empire"] = ["Empire", "The Empire", "Human", "Humans"],
        ["estalia"] = ["Estalia", "Estalians", "Human", "Humans"],
        ["grand-cathay"] = ["Grand Cathay", "Cathay", "Cathayans", "Human", "Humans"],
        ["halflings"] = ["Halflings", "Halfling"],
        ["high-elves"] = ["High Elves", "High Elf", "Elves", "Elf"],
        ["hobgoblins"] = ["Hobgoblins", "Hobgoblin", "Goblins", "Goblin", "Greenskins"],
        ["kingdoms-of-ind"] = ["Kingdoms of Ind", "Ind", "Human", "Humans"],
        ["kislev"] = ["Kislev", "Kislevites", "Human", "Humans"],
        ["lizardmen"] = ["Lizardmen", "Lizardman"],
        ["nippon"] = ["Nippon", "Nipponese", "Human", "Humans"],
        ["norsca"] = ["Norsca", "Norse", "Norscans", "Human", "Humans", "Chaos"],
        ["ogre-kingdoms"] = ["Ogres", "Ogre", "Ogre Kingdoms"],
        ["orcs-goblins"] = ["Orcs", "Orc", "Goblins", "Goblin", "Greenskins", "Trolls", "Troll"],
        ["pirates-of-sartosa"] = ["Pirates of Sartosa", "Sartosa", "Pirates", "Human", "Humans"],
        ["skaven"] = ["Skaven"],
        ["tomb-kings"] = ["Tomb Kings", "Undead"],
        ["vampire-counts"] = ["Vampire Counts", "Undead", "Vampires"],
        ["warriors-of-chaos"] = ["Warriors of Chaos", "Chaos"],
        ["wood-elves"] = ["Wood Elves", "Wood Elf", "Elves", "Elf"],
        ["zombie-pirates"] = ["Zombie Pirates", "Undead", "Vampires", "Pirates"],
    };

    private static readonly HashSet<string> ElfFactions =
        new(["dark-elves", "high-elves", "wood-elves"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Check if the opponent faction matches a Hatred target string.
    /// Target can be comma-separated (e.g., "Orcs, Goblins, Trolls, Skaven").
    /// Returns true if any target keyword matches any of the opponent's race keywords.
    /// </summary>
    public static bool HatredApplies(string? hatredTarget, string? opponentFactionKey)
    {
        if (string.IsNullOrWhiteSpace(hatredTarget) || string.IsNullOrWhiteSpace(opponentFactionKey))
            return true;

        if (!RaceKeywords.TryGetValue(opponentFactionKey, out var opponentRaces))
            return false;

        var targets = hatredTarget.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var target in targets)
        {
            foreach (var race in opponentRaces)
            {
                if (string.Equals(target, race, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Also check direct faction key match (e.g., "Forces of Destruction")
            if (target.Equals("Forces of Destruction", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if the opponent faction is an Elf faction (for Fear Elves rule).
    /// </summary>
    public static bool IsElf(string? factionKey) =>
        factionKey != null && ElfFactions.Contains(factionKey);

    /// <summary>Every faction id the table knows.</summary>
    public static IEnumerable<string> KnownFactions => RaceKeywords.Keys;
}
