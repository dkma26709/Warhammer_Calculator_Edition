namespace Warhammer.Models.Json;

/// <summary>
/// The active system's <c>troop-types.json</c>: the figures the Troop Types chapter states for
/// each troop type. Hand-maintained per system, like <see cref="CompositionData"/> — the
/// chapter is prose, so <c>extract_core_rules.py</c> cannot lift it into core-rules.json.
/// </summary>
public record TroopTypesData
{
    public string? Source { get; init; }
    public List<string> Notes { get; init; } = [];

    /// <summary>Keyed by kebab-case troop type, e.g. "monstrous-cavalry".</summary>
    public Dictionary<string, TroopTypeEntryData> Types { get; init; } = new();
}

public record TroopTypeEntryData
{
    /// <summary>Models wide required for a complete rank.</summary>
    public int MinRankWidth { get; init; }

    /// <summary>Supporting attacks for the rider, or for the whole model if it has no mount.</summary>
    public int SupportingAttacks { get; init; }

    /// <summary>Supporting attacks for the mount. Split-profile types only; zero in wap-v3.</summary>
    public int MountSupportingAttacks { get; init; }

    /// <summary>The book's separate figure for a model of this type without a split profile.</summary>
    public int NonSplitSupportingAttacks { get; init; }

    /// <summary>Impact Hits per front-rank model from the troop type alone.</summary>
    public int ImpactHits { get; init; }

    public int UnitStrength { get; init; }
    public bool SplitProfile { get; init; }

    /// <summary>Whether the troop type itself grants Swiftstride. False throughout wap-v3.</summary>
    public bool Swiftstride { get; init; }
}
