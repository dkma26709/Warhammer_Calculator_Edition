namespace Warhammer.Models.Json;

public record FactionExtrasData
{
    public string FactionName { get; init; } = "";
    public List<ArmyRuleEntry> ArmySpecialRules { get; init; } = [];
    public List<MagicItemEntry> MagicItems { get; init; } = [];
    public List<LoreEntry> LoresOfMagic { get; init; } = [];
    public Dictionary<string, List<UpgradeEntry>> FactionUpgrades { get; init; } = new();
}

public record ArmyRuleEntry
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
}

public record MagicItemEntry
{
    public string Name { get; init; } = "";
    public string Points { get; init; } = "0";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Restriction { get; init; }
}

public record LoreEntry
{
    public string Name { get; init; } = "";
    public string? Attribute { get; init; }
    public List<SpellEntry> Spells { get; init; } = [];
}

public record SpellEntry
{
    public string Name { get; init; } = "";
    public int Level { get; init; }
    public int CastValue { get; init; }
    public string Description { get; init; } = "";
    public string? Type { get; init; }
}

public record UpgradeEntry
{
    public string Name { get; init; } = "";
    public string Points { get; init; } = "0";
    public string Description { get; init; } = "";
}
