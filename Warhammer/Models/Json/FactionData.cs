using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>One army book from the bundle: its rules, items, upgrades, lores and units.</summary>
public record FactionData
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";

    /// <summary>order, destruction or neutral.</summary>
    public string? Align { get; init; }

    public List<FactionRuleEntry> Rules { get; init; } = [];
    public List<MagicItemEntry> Items { get; init; } = [];
    public List<UpgradeEntry> Upgrades { get; init; } = [];
    public List<LoreEntry> Lores { get; init; } = [];

    /// <summary>Present when the book states constraints of its own; holds only <c>unitConstraints</c>.</summary>
    public CompositionData? Composition { get; init; }

    public List<UnitEntry> Units { get; init; } = [];
}

public record FactionRuleEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>
/// One entry of a faction's upgrade chapter — a Knightly Order, a Blessed Spawning, a rune.
/// Its family is the chapter it sits in, which option groups name by kebab-case id.
/// </summary>
public record UpgradeEntry
{
    public string Id { get; init; } = "";
    public string Chapter { get; init; } = "";

    /// <summary>A heading within the chapter, when it has them: "WEAPON RUNES".</summary>
    public string? Group { get; init; }

    public string Name { get; init; } = "";

    [JsonConverter(typeof(UpgradeCostConverter))]
    public UpgradeCost? Cost { get; init; }

    [JsonConverter(typeof(LenientStringConverter))]
    public string? Only { get; init; }

    [JsonConverter(typeof(LenientStringConverter))]
    public string? Bound { get; init; }

    public bool OneUse { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>
/// An upgrade's price: flat, or tiered by who takes it. Exactly one of the two is set.
/// </summary>
public record UpgradeCost
{
    public double? Flat { get; init; }
    public List<UpgradeCostTier> Tiers { get; init; } = [];
}

/// <summary>"Cavalry/Infantry" → "1 point per model".</summary>
public record UpgradeCostTier
{
    public string Scope { get; init; } = "";
    public string Text { get; init; } = "";
}
