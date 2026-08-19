using System.Text.Json.Serialization;

namespace Warhammer.Models.Domain;

/// <summary>How an overridden composition limit is expressed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LimitUnit>))]
public enum LimitUnit
{
    /// <summary>A share of the army's points target, stored as the user types it (15 = 15%).</summary>
    Percent,

    /// <summary>A fixed points figure, independent of the army's points target.</summary>
    Points,
}

/// <summary>One overridden limit — "15%" or "300 pts".</summary>
public sealed record LimitOverride
{
    public required double Value { get; init; }
    public required LimitUnit Unit { get; init; }

    public double ToPoints(int pointsTarget) =>
        Unit == LimitUnit.Points ? Value : pointsTarget * Value / 100.0;

    /// <summary>For validation messages.</summary>
    public string Describe(int pointsTarget) =>
        Unit == LimitUnit.Points ? $"{Value:F0} pts" : $"{Value:F0}% of {pointsTarget}";
}

/// <summary>A limit in force, already reduced to points.</summary>
public sealed record EffectiveLimit
{
    public required double Points { get; init; }

    /// <summary>Where the figure came from, e.g. "25% of 2000" or "300 pts".</summary>
    public required string Description { get; init; }
}

/// <summary>
/// Every composition limit for one army list, resolved against its points target.
/// A null limit is one the system does not impose and the list has not added.
/// </summary>
public sealed record EffectiveLimits
{
    public EffectiveLimit? LordsMax { get; init; }
    public EffectiveLimit? HeroesMax { get; init; }
    public required EffectiveLimit CharactersCombinedMax { get; init; }
    public EffectiveLimit? SingleUnitMax { get; init; }
    public required EffectiveLimit CoreMin { get; init; }
    public required EffectiveLimit SpecialMax { get; init; }
    public required EffectiveLimit RareMax { get; init; }
    public required int SpecialDuplicateLimit { get; init; }
    public required int RareDuplicateLimit { get; init; }
}

/// <summary>
/// A game system's army composition percentages, loaded from
/// <c>wwwroot/data/{system}/composition.json</c> and stored as fractions (0.25 = 25%).
/// </summary>
/// <remarks>
/// A null cap means the system has no such limit. wap-v2 splits characters into Lords and
/// Heroes with a 25% cap each; wap-v3 merges them into one Characters section, so it carries
/// no per-section caps and <see cref="CharactersCombinedMaxPercent"/> is the only sectional
/// constraint on characters.
/// </remarks>
public sealed record CompositionRules
{
    public double? LordsMaxPercent { get; init; }
    public double? HeroesMaxPercent { get; init; }
    public required double CharactersCombinedMaxPercent { get; init; }

    /// <summary>Cap on any one unit's full cost — characters included, mounts and magic items counted.</summary>
    public double? SingleUnitMaxPercent { get; init; }

    public required double CoreMinPercent { get; init; }
    public required double SpecialMaxPercent { get; init; }
    public required double RareMaxPercent { get; init; }

    /// <summary>
    /// The limits actually in force for a list: its overrides where set, this system's
    /// percentages everywhere else. The single place override precedence is decided, so the
    /// validator and the builder's progress bars cannot disagree.
    /// </summary>
    public EffectiveLimits Resolve(CompositionOverrides o, int pointsTarget)
    {
        EffectiveLimit? Limit(double? systemPercent, LimitOverride? overridden)
        {
            if (overridden is not null)
                return new EffectiveLimit
                {
                    Points = overridden.ToPoints(pointsTarget),
                    Description = overridden.Describe(pointsTarget),
                };

            if (systemPercent is not { } percent)
                return null;

            return new EffectiveLimit
            {
                Points = pointsTarget * percent,
                // Matches LimitOverride.Describe so system and house limits read the same way.
                Description = $"{percent * 100:F0}% of {pointsTarget}",
            };
        }

        // Required limits: every shipped system declares these, so Limit() cannot return null.
        var characters = Limit(CharactersCombinedMaxPercent, o.CharactersCombinedMax)!;
        var core = Limit(CoreMinPercent, o.CoreMin)!;
        var special = Limit(SpecialMaxPercent, o.SpecialMax)!;
        var rare = Limit(RareMaxPercent, o.RareMax)!;

        var (bracketSpecial, bracketRare) = ArmyCompositionRules.DuplicateLimits(pointsTarget);

        return new EffectiveLimits
        {
            LordsMax = Limit(LordsMaxPercent, o.LordsMax),
            HeroesMax = Limit(HeroesMaxPercent, o.HeroesMax),
            CharactersCombinedMax = characters,
            SingleUnitMax = Limit(SingleUnitMaxPercent, o.SingleUnitMax),
            CoreMin = core,
            SpecialMax = special,
            RareMax = rare,
            SpecialDuplicateLimit = o.SpecialDuplicateLimit ?? bracketSpecial,
            RareDuplicateLimit = o.RareDuplicateLimit ?? bracketRare,
        };
    }
}

/// <summary>
/// Per-list adjustments to the system's composition rules — a house or tournament ruleset,
/// e.g. Rare dropped to 15%, or a flat 300-point ceiling. Null means "use the system's value".
/// </summary>
/// <remarks>
/// These belong to the army list and are stored nowhere else: the active-list autosave and
/// JSON export carry them, and there is no app-level composition setting.
/// </remarks>
public sealed record CompositionOverrides
{
    public LimitOverride? LordsMax { get; init; }
    public LimitOverride? HeroesMax { get; init; }
    public LimitOverride? CharactersCombinedMax { get; init; }
    public LimitOverride? SingleUnitMax { get; init; }
    public LimitOverride? CoreMin { get; init; }
    public LimitOverride? SpecialMax { get; init; }
    public LimitOverride? RareMax { get; init; }

    /// <summary>Replaces the points-bracket figure from <see cref="ArmyCompositionRules.DuplicateLimits"/>.</summary>
    public int? SpecialDuplicateLimit { get; init; }
    public int? RareDuplicateLimit { get; init; }

    /// <summary>
    /// The cap's name in saved lists from before it covered non-character units. Read-only:
    /// deserialising an old list lands the value on <see cref="SingleUnitMax"/>, and the null
    /// getter keeps it out of everything written from now on.
    /// </summary>
    [JsonPropertyName("singleCharacterMax")]
    public LimitOverride? LegacySingleCharacterMax
    {
        get => null;
        init => SingleUnitMax ??= value;
    }

    public bool IsEmpty =>
        LordsMax is null && HeroesMax is null && CharactersCombinedMax is null
        && SingleUnitMax is null && CoreMin is null && SpecialMax is null && RareMax is null
        && SpecialDuplicateLimit is null && RareDuplicateLimit is null;
}
