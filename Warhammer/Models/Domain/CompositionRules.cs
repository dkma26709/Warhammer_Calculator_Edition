using System.Text.Json.Serialization;
using Warhammer.Models.Json;

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
/// The duplicate chart for one section: a cap per points bracket, and how the cap grows past
/// the last bracket. Read from the bundle's composition block.
/// </summary>
public sealed record DuplicateRule
{
    public static readonly DuplicateRule Unlimited = new();

    public IReadOnlyList<DuplicateBand> Bands { get; init; } = [];
    public int? BeyondEveryPoints { get; init; }
    public int BeyondAdd { get; init; }

    /// <summary>How many of one unit a list at this points target may field.</summary>
    public int LimitFor(int pointsTarget)
    {
        if (Bands.Count == 0) return int.MaxValue;

        var band = Bands.FirstOrDefault(b => pointsTarget >= b.MinPoints && pointsTarget <= b.MaxPoints);
        if (band is not null) return band.Max;

        var last = Bands.OrderBy(b => b.MaxPoints).Last();
        if (pointsTarget < last.MaxPoints) return Bands.OrderBy(b => b.MinPoints).First().Max;

        if (BeyondEveryPoints is not { } every || every <= 0) return last.Max;

        // The last band ends at 4999 and the first step past it is 5000-5999, so the steps are
        // counted from the point just past the band, not from its ceiling.
        int beyond = (pointsTarget - (last.MaxPoints + 1)) / every;
        return last.Max + (beyond + 1) * BeyondAdd;
    }

    public static DuplicateRule FromData(DuplicateRuleEntry? entry)
    {
        if (entry is null) return Unlimited;
        return new DuplicateRule
        {
            Bands = entry.Bands.Select(b => new DuplicateBand(b.MinPoints, b.MaxPoints, b.Max)).ToList(),
            BeyondEveryPoints = entry.Beyond?.EveryPoints,
            BeyondAdd = entry.Beyond?.Add ?? 0,
        };
    }
}

public sealed record DuplicateBand(int MinPoints, int MaxPoints, int Max);

/// <summary>
/// A game system's army composition rules, read from the bundle's composition block and stored
/// as fractions (0.25 = 25%), with the constraints a faction's own book adds.
/// </summary>
/// <remarks>
/// A null cap means the system has no such limit. A book that splits characters into Lords and
/// Heroes states a cap for each; one that merges them carries no per-section caps and
/// <see cref="CharactersCombinedMaxPercent"/> is the only sectional constraint on characters.
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

    /// <summary>Whether the army must field a General — every published system says so.</summary>
    public bool GeneralRequired { get; init; } = true;

    /// <summary>How many Battle Standard Bearers an army may have.</summary>
    public int BattleStandardMax { get; init; } = 1;

    /// <summary>How many of each special character an army may field.</summary>
    public int SpecialCharactersMaxEach { get; init; } = 1;

    public DuplicateRule SpecialDuplicates { get; init; } = DuplicateRule.Unlimited;
    public DuplicateRule RareDuplicates { get; init; } = DuplicateRule.Unlimited;

    /// <summary>The unit constraints in force: the system's, then the faction's own.</summary>
    public IReadOnlyList<UnitConstraintEntry> UnitConstraints { get; init; } = [];

    /// <summary>
    /// The rules for a faction: the bundle's top-level block, with the faction's own
    /// constraints appended. Core, Special and Rare are required because every system so far
    /// constrains them; Lords and Heroes are not.
    /// </summary>
    public static CompositionRules FromData(CompositionData system, CompositionData? faction, string systemKey)
    {
        double? Max(string category) =>
            system.PointsCategories.TryGetValue(category, out var entry) ? entry.MaxPercent : null;
        double? Min(string category) =>
            system.PointsCategories.TryGetValue(category, out var entry) ? entry.MinPercent : null;

        double Require(double? value, string what) => value
            ?? throw new InvalidOperationException(
                $"The composition block for '{systemKey}' is missing {what}.");

        var constraints = new List<UnitConstraintEntry>(system.UnitConstraints);
        if (faction?.UnitConstraints is { Count: > 0 } own)
            constraints.AddRange(own);

        return new CompositionRules
        {
            LordsMaxPercent = Max("lords"),
            HeroesMaxPercent = Max("heroes"),
            CharactersCombinedMaxPercent = Require(Max("characters"), "pointsCategories.characters.maxPercent"),
            SingleUnitMaxPercent = system.SingleUnitMaxPercent,
            CoreMinPercent = Require(Min("core"), "pointsCategories.core.minPercent"),
            SpecialMaxPercent = Require(Max("special"), "pointsCategories.special.maxPercent"),
            RareMaxPercent = Require(Max("rare"), "pointsCategories.rare.maxPercent"),
            GeneralRequired = system.General?.Required ?? true,
            BattleStandardMax = system.BattleStandard?.Max ?? 1,
            SpecialCharactersMaxEach = system.SpecialCharacters?.MaxEach ?? 1,
            SpecialDuplicates = DuplicateRule.FromData(system.Duplicates.GetValueOrDefault("special")),
            RareDuplicates = DuplicateRule.FromData(system.Duplicates.GetValueOrDefault("rare")),
            UnitConstraints = constraints,
        };
    }

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

        return new EffectiveLimits
        {
            LordsMax = Limit(LordsMaxPercent, o.LordsMax),
            HeroesMax = Limit(HeroesMaxPercent, o.HeroesMax),
            CharactersCombinedMax = characters,
            SingleUnitMax = Limit(SingleUnitMaxPercent, o.SingleUnitMax),
            CoreMin = core,
            SpecialMax = special,
            RareMax = rare,
            SpecialDuplicateLimit = o.SpecialDuplicateLimit ?? SpecialDuplicates.LimitFor(pointsTarget),
            RareDuplicateLimit = o.RareDuplicateLimit ?? RareDuplicates.LimitFor(pointsTarget),
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

    /// <summary>Replaces the points-bracket figure from the system's duplicate chart.</summary>
    public int? SpecialDuplicateLimit { get; init; }
    public int? RareDuplicateLimit { get; init; }

    public bool IsEmpty =>
        LordsMax is null && HeroesMax is null && CharactersCombinedMax is null
        && SingleUnitMax is null && CoreMin is null && SpecialMax is null && RareMax is null
        && SpecialDuplicateLimit is null && RareDuplicateLimit is null;
}
