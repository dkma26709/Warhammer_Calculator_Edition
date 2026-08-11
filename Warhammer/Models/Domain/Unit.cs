using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Models.Domain;

/// <summary>
/// A fully-resolved army book unit entry — the template for what can be fielded.
/// Composed of one or more <see cref="Model"/>s plus unit-level data.
/// Immutable — user configuration lives in <see cref="UnitConfiguration"/>.
/// </summary>
public sealed record Unit
{
    /// <summary>Unit name as it appears in the army book.</summary>
    public required string Name { get; init; }

    /// <summary>Which faction this unit belongs to.</summary>
    public required FactionKey Faction { get; init; }

    /// <summary>Army book category (Lords, Heroes, Core, etc.).</summary>
    public required UnitCategory Category { get; init; }

    /// <summary>The regular trooper or character model.</summary>
    public required Model BaseModel { get; init; }

    /// <summary>Mount model for cavalry/monstrous cavalry (null if unmounted).</summary>
    public Model? Mount { get; init; }

    /// <summary>Base width in mm (parsed from baseSize field).</summary>
    public required int BaseWidthMm { get; init; }

    /// <summary>Available options (equipment, command, mount, magic items, etc.).</summary>
    public required IReadOnlyList<UnitOption> Options { get; init; }

    /// <summary>Minimum unit size (from unitSize field). Characters default to 1.</summary>
    public required int MinUnitSize { get; init; }

    /// <summary>Maximum unit size, if specified.</summary>
    public int? MaxUnitSize { get; init; }

    /// <summary>Points cost per model (base cost before upgrades).</summary>
    public required double BaseModelCost { get; init; }

    /// <summary>Pre-computed command group availability and costs.</summary>
    public required CommandAvailability Command { get; init; }

    /// <summary>Points limit for magic item selection (null if not a magic item bearer).</summary>
    public int? MagicItemAllowance { get; init; }

    /// <summary>Wizard level (null if not a wizard).</summary>
    public int? WizardLevel { get; init; }

    /// <summary>
    /// Full armour lookup table for resolving equipment upgrades.
    /// Set by the repository — contains all armour types, not just this unit's base equipment.
    /// </summary>
    public required IReadOnlyDictionary<ArmourKey, Armour> AvailableArmour { get; init; }

    /// <summary>The raw <see cref="UnitEntry"/> from JSON.</summary>
    public required UnitEntry RawEntry { get; init; }

    /// <summary>
    /// The magic item budget an entry may spend. An army book prints one options block for a
    /// page of entries, so a Captain's allowance and a General's both land on both units; the
    /// name on the line is what separates them. An unnamed allowance belongs to whoever holds
    /// it. When only a neighbour's allowances are present, take the smallest — the entry can
    /// still be equipped, without handing it a budget the book never gave it.
    /// </summary>
    public static int? ResolveMagicItemAllowance(string entryName, IEnumerable<UnitOption>? options)
    {
        var allowances = options?
            .Where(o => o.IsType(OptionType.MagicItems))
            .ToList();
        if (allowances is null || allowances.Count == 0)
            return null;

        var mine = allowances.FirstOrDefault(o =>
            string.Equals(o.AppliesTo?.Trim(), entryName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (mine != null)
            return mine.PointsLimit;

        var unnamed = allowances.FirstOrDefault(o => o.AppliesTo is null);
        if (unnamed != null)
            return unnamed.PointsLimit;

        return allowances.Min(o => o.PointsLimit);
    }
}

/// <summary>
/// Pre-computed command group availability and costs for a unit.
/// </summary>
public sealed record CommandAvailability
{
    public static CommandAvailability None { get; } = new();

    public bool CanHaveChampion { get; init; }
    public double ChampionCost { get; init; }
    public bool CanHaveMusician { get; init; }
    public double MusicianCost { get; init; }
    public bool CanHaveStandardBearer { get; init; }
    public double StandardBearerCost { get; init; }
}
