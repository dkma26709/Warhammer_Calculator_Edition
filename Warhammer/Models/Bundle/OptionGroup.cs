using System.Text.Json.Serialization;

namespace Warhammer.Models.Bundle;

/// <summary>
/// One line of a unit's OPTIONS list, typed. A line that ends in "the following:" is a menu and
/// the lines under it are its <see cref="Choices"/>; any other line is a group of one choice,
/// itself. Every group and choice keeps the printed line in <c>Raw</c>.
/// </summary>
public record OptionGroup
{
    /// <summary>kebab-case, unique within the unit across every nesting level.</summary>
    public string Id { get; init; } = "";

    /// <summary>equipment, mount, command, magicStandard, magicItems, battleStandard, wizardLevel, factionUpgrade, specialRule, upgrade.</summary>
    public string Kind { get; init; } = "";

    /// <summary>one (exclusive), any (independent toggles) or count (taken several times).</summary>
    public string Select { get; init; } = "";

    public int Min { get; init; }
    public int? Max { get; init; }

    /// <summary>The profile the line restricts itself to, by id, or null for the whole entry.</summary>
    public string? AppliesTo { get; init; }

    public string Raw { get; init; } = "";

    /// <summary>A bracketed qualifier naming no profile: "if armed with heavy lances".</summary>
    public string? Condition { get; init; }

    /// <summary>True when the classifier could not place the line; the kind is then upgrade.</summary>
    public bool Unclassified { get; init; }

    // command
    /// <summary>champion, musician or standardBearer.</summary>
    public string? Role { get; init; }

    // magicStandard, magicItems, factionUpgrade
    public int? PointsLimit { get; init; }

    // magicItems
    /// <summary>The item categories the allowance admits, or null for any.</summary>
    public List<string>? ItemTypes { get; init; }

    /// <summary>Upgrade families the same allowance also pays for — "one Virtue and/or Magic Items".</summary>
    public List<string>? Families { get; init; }

    // factionUpgrade
    /// <summary>The family's id — the faction's upgrade chapter, in kebab-case.</summary>
    public string? Family { get; init; }

    // specialRule
    public string? Rule { get; init; }

    // battleStandard
    public double? Points { get; init; }

    public List<OptionChoice> Choices { get; init; } = [];
}

public record OptionChoice
{
    /// <summary>kebab-case, unique within the group.</summary>
    public string Id { get; init; } = "";

    /// <summary>The thing offered, without the verb phrase or the price: "Great weapon".</summary>
    public string Name { get; init; } = "";

    /// <summary>A number; 0 for "free"; null when the line prices nothing (an allowance).</summary>
    [JsonConverter(typeof(NullableDoubleConverter))]
    public double? Points { get; init; }

    public bool PerModel { get; init; }

    /// <summary>What the price is per when it is not the model: "Crew", "Genie".</summary>
    public string? Per { get; init; }

    public string Raw { get; init; } = "";

    /// <summary>A profile id from a bracketed restriction on the choice itself: "Griffon (General only)".</summary>
    public string? AppliesTo { get; init; }

    public string? Condition { get; init; }

    /// <summary>What the choice replaces, from "May replace X with Y".</summary>
    public string? Replaces { get; init; }

    // select: count
    public int? Min { get; init; }
    public int? Max { get; init; }

    /// <summary>"one Fanatic for every 10 models": a count that scales with the unit.</summary>
    public int? EveryModels { get; init; }

    /// <summary>Groups that only apply once this choice is taken — a standard bearer's magic standard.</summary>
    public List<OptionGroup>? Options { get; init; }

    /// <summary>On a mount choice, the id of the mount's own entry in the book.</summary>
    public string? UnitId { get; init; }

    // wizardLevel
    public int? Level { get; init; }
    public int? LevelDelta { get; init; }

    // specialRule, upgrade
    public string? Rule { get; init; }
}
