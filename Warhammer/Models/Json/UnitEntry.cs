using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>
/// One army list entry as the bundle prints it. The book's own fields keep their exact text;
/// the parsed companions beside them — <see cref="Section"/>, <see cref="UnitSizeParsed"/>,
/// <see cref="OptionGroups"/> — are what the app reads.
/// </summary>
public record UnitEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Chapter { get; init; }

    /// <summary>The chapter's section as printed: "Characters", "Core", "Mounts".</summary>
    public string? Category { get; init; }

    /// <summary>The army-list slot the category fills: characters, core, special, rare, mounts; null for none.</summary>
    public string? Section { get; init; }

    /// <summary>"lord" or "hero" for a book that splits its characters, else null.</summary>
    public string? Tier { get; init; }

    /// <summary>True under the book's SPECIAL CHARACTERS heading.</summary>
    public bool Named { get; init; }

    public List<ProfileEntry> Profiles { get; init; } = [];

    public string? UnitSize { get; init; }
    public UnitSizeEntry? UnitSizeParsed { get; init; }

    public string TroopType { get; init; } = "";
    public string? BaseSize { get; init; }

    public string? Equipment { get; init; }
    public List<string>? EquipmentList { get; init; }

    [JsonConverter(typeof(StringOrStringListConverter))]
    public string? SpecialRules { get; init; }

    public List<string>? SpecialRulesList { get; init; }

    /// <summary>The wizard paragraph, where the entry casts: levels and the lores it may use.</summary>
    public string? Magic { get; init; }

    /// <summary>The entry's own UPGRADES field — the things its options may refer to by name.</summary>
    [JsonConverter(typeof(StringOrStringListConverter))]
    public string? Upgrades { get; init; }

    /// <summary>The book's NOTES under the entry, one string per line; a note the book set as a heading with text is read by its text.</summary>
    [JsonConverter(typeof(LenientStringListConverter))]
    public List<string>? Notes { get; init; }

    [JsonConverter(typeof(LenientStringConverter))]
    public string? Crew { get; init; }

    [JsonConverter(typeof(LenientStringConverter))]
    public string? Mount { get; init; }

    [JsonConverter(typeof(LenientStringConverter))]
    public string? DrawnBy { get; init; }

    public List<OptionGroup> OptionGroups { get; init; } = [];
}

public record UnitSizeEntry
{
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
}

public record ProfileEntry
{
    /// <summary>kebab-case, unique within the unit.</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    [JsonConverter(typeof(NullableIntConverter))]
    public int? M { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? WS { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? BS { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? S { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? T { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? W { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? I { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? A { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? Ld { get; init; }

    /// <summary>Points per model. Null for a profile that is not bought on its own — a mount, a crew line.</summary>
    [JsonConverter(typeof(NullableDoubleConverter))]
    public double? Points { get; init; }
}
