namespace Warhammer.Models.Json;

/// <summary>
/// Army composition as the bundle states it: the top-level block is read from the rulebook's
/// army-selection chapter, and a faction's own block holds only the unit constraints its book
/// prints. A value the source does not state is absent, never guessed.
/// </summary>
public record CompositionData
{
    /// <summary>Keyed by section: characters, lords, heroes, core, special, rare.</summary>
    public Dictionary<string, CategoryLimitEntry> PointsCategories { get; init; } = new();

    public double? SingleUnitMaxPercent { get; init; }
    public GeneralRuleEntry? General { get; init; }
    public BattleStandardRuleEntry? BattleStandard { get; init; }
    public SpecialCharactersRuleEntry? SpecialCharacters { get; init; }

    /// <summary>Keyed by section — special, rare — the duplicate chart for that section.</summary>
    public Dictionary<string, DuplicateRuleEntry> Duplicates { get; init; } = new();

    public List<UnitConstraintEntry> UnitConstraints { get; init; } = [];

    /// <summary>The sentence each value was read from, keyed by its path in this block.</summary>
    public Dictionary<string, string>? Sources { get; init; }
}

public record CategoryLimitEntry
{
    public double? MinPercent { get; init; }
    public double? MaxPercent { get; init; }
    public ExpendableRuleEntry? Expendable { get; init; }
}

public record ExpendableRuleEntry
{
    public int? RequiresOtherCore { get; init; }
}

public record GeneralRuleEntry
{
    public bool Required { get; init; }
}

public record BattleStandardRuleEntry
{
    public int? Max { get; init; }
}

public record SpecialCharactersRuleEntry
{
    public int? MaxEach { get; init; }
}

/// <summary>A band per points bracket, then a rule for growth past the last band.</summary>
public record DuplicateRuleEntry
{
    public List<DuplicateBandEntry> Bands { get; init; } = [];
    public DuplicateBeyondEntry? Beyond { get; init; }
}

public record DuplicateBandEntry
{
    public int MinPoints { get; init; }
    public int MaxPoints { get; init; }
    public int Max { get; init; }
}

public record DuplicateBeyondEntry
{
    public int EveryPoints { get; init; }
    public int Add { get; init; }
}

/// <summary>
/// One constraint a book states under a unit's notes. <see cref="Type"/> says which fields
/// apply: max, limited, perSlot, ratio, general or handlers.
/// </summary>
public record UnitConstraintEntry
{
    public string Type { get; init; } = "";
    public List<string> UnitIds { get; init; } = [];
    public string Raw { get; init; } = "";

    // max / limited
    public int? Min { get; init; }
    public int? Max { get; init; }
    public int? PerPoints { get; init; }

    /// <summary>When the note counts one profile of the unit rather than the unit — "2 Arch Lectors".</summary>
    public string? ProfileId { get; init; }

    // perSlot
    public PerSlotEntry? PerSlot { get; init; }
    public string? Section { get; init; }

    // ratio
    public bool? PerUnit { get; init; }

    /// <summary>The units the constrained unit is counted against, by id.</summary>
    public List<string>? Requires { get; init; }

    /// <summary>True when any one of <see cref="Requires"/> satisfies the ratio, rather than all.</summary>
    public bool? RequiresAny { get; init; }

    /// <summary>Names in the note that resolved to no unit id.</summary>
    public List<string>? RequiresNames { get; init; }

    /// <summary>A special rule the counted units must have — "units with the State Troops special rule".</summary>
    public string? RequiresRule { get; init; }

    // general
    public bool? MustBeGeneral { get; init; }
    public bool? MayBeGeneral { get; init; }

    // handlers
    public HandlersEntry? Handlers { get; init; }
}

public record PerSlotEntry
{
    public int Min { get; init; }
    public int Max { get; init; }
}

public record HandlersEntry
{
    public string Name { get; init; } = "";
    public int Per { get; init; }
}
