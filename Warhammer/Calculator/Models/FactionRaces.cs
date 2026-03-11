namespace Warhammer.Calculator.Models;

/// <summary>
/// Maps faction keys to race keywords used for conditional rules
/// like Hatred (target) and Fear Elves.
/// </summary>
public static class FactionRaces
{
    private static readonly Dictionary<string, HashSet<string>> RaceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["beastmen"] = ["Beastmen", "Beastman"],
        ["bretonnia"] = ["Bretonnia", "Bretonnians", "Human"],
        ["daemons-of-chaos"] = ["Daemons", "Daemon", "Daemons of Chaos"],
        ["dark-elves"] = ["Dark Elves", "Dark Elf", "Elves", "Elf"],
        ["dwarfs"] = ["Dwarfs", "Dwarf", "Dwarves"],
        ["high-elves"] = ["High Elves", "High Elf", "Elves", "Elf"],
        ["lizardmen"] = ["Lizardmen", "Lizardman"],
        ["ogre-kingdoms"] = ["Ogres", "Ogre", "Ogre Kingdoms"],
        ["orcs-and-goblins"] = ["Orcs", "Orc", "Goblins", "Goblin", "Greenskins", "Trolls", "Troll"],
        ["skaven"] = ["Skaven"],
        ["the-empire"] = ["Empire", "The Empire", "Human"],
        ["tomb-kings"] = ["Tomb Kings", "Undead"],
        ["vampire-counts"] = ["Vampire Counts", "Undead", "Vampires"],
        ["warriors-of-chaos"] = ["Warriors of Chaos", "Chaos"],
        ["wood-elves"] = ["Wood Elves", "Wood Elf", "Elves", "Elf"],
    };

    private static readonly HashSet<string> ElfFactions =
        ["dark-elves", "high-elves", "wood-elves"];

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
}
