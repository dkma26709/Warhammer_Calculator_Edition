using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>
/// The whole edition as one file — <c>wwwroot/data/{system}/{system}.json</c>, formatVersion 2.
/// Exported by <c>export.py</c> in the warhammer-armies-revamped repo and copied here on a
/// release; <see cref="Source"/> is the commit it came from. Generated, never hand-edited.
/// </summary>
/// <remarks>
/// Factions stay as raw JSON until one is asked for: the file holds thirty books and a page
/// needs one, so materialising every unit up front would cost seconds of parsing on load for
/// nothing. <c>BundleService</c> deserialises a faction on first use and caches it.
/// </remarks>
public record BundleData
{
    public int FormatVersion { get; init; }
    public string System { get; init; } = "";
    public string Version { get; init; } = "";
    public string? Source { get; init; }
    public RulebookData Rulebook { get; init; } = new();
    public CompositionData? Composition { get; init; }
    public Dictionary<string, JsonElement> Factions { get; init; } = new();
}

/// <summary>The rulebook with its rules, magic items, lores and weapon tables lifted out.</summary>
public record RulebookData
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";

    /// <summary>Special rules by their printed heading — "ALWAYS STRIKES FIRST" — to their text.</summary>
    public Dictionary<string, string> Rules { get; init; } = new();

    /// <summary>The common magic items every army may take.</summary>
    public List<MagicItemEntry> Items { get; init; } = [];

    public List<LoreEntry> Lores { get; init; } = [];

    /// <summary>The weapon and armour tables as printed. The engine reads its own core-rules.json instead.</summary>
    public List<WeaponTextEntry> Weapons { get; init; } = [];
}

public record WeaponTextEntry
{
    public string Section { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>A magic item, from the rulebook's common list or a faction's own.</summary>
public record MagicItemEntry
{
    public string Id { get; init; } = "";

    /// <summary>weapon, armour, talisman, arcane, enchanted or standard — see <c>MagicItemCategories</c>.</summary>
    public string Category { get; init; } = "";

    public string Name { get; init; } = "";

    [JsonConverter(typeof(NullableDoubleConverter))]
    public double? Cost { get; init; }

    /// <summary>What the item is in the mundane sense: "Hand weapon", "Heavy armour", "Charm".</summary>
    public string? Type { get; init; }

    /// <summary>Who may take it, when the book restricts it: "General", "Model on foot", "Wizard".</summary>
    public string? Only { get; init; }

    /// <summary>The bound spell, when the item carries one: the book's text, or "true" where it only flags it.</summary>
    [JsonConverter(typeof(LenientStringConverter))]
    public string? Bound { get; init; }

    public bool OneUse { get; init; }
    public bool Common { get; init; }
    public string? Chapter { get; init; }
    public string Text { get; init; } = "";
}

public record LoreEntry
{
    public string? Id { get; init; }
    public string Name { get; init; } = "";
    public List<SpellEntry> Spells { get; init; } = [];
}

public record SpellEntry
{
    public string Name { get; init; } = "";

    /// <summary>"1" to "4" for a levelled spell, "Signature Spell" or "Lore Attribute" otherwise.</summary>
    [JsonConverter(typeof(StringOrNumberConverter))]
    public string? Level { get; init; }

    /// <summary>The casting value as printed, "6+"; null for a lore attribute.</summary>
    public string? Cast { get; init; }

    public string Text { get; init; } = "";
}
