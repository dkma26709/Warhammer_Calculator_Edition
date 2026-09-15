using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Models.Domain;

/// <summary>
/// A fully-resolved army book entry — the template for what can be fielded. Composed of one or
/// more <see cref="Model"/>s plus unit-level data. Immutable — user configuration lives in
/// <see cref="UnitConfiguration"/>.
/// </summary>
public sealed record Unit
{
    /// <summary>The bundle's id for the entry, unique within the faction and stable across releases.</summary>
    public required string Id { get; init; }

    /// <summary>Unit name as it appears in the army book.</summary>
    public required string Name { get; init; }

    /// <summary>The faction's bundle id.</summary>
    public required string FactionId { get; init; }

    /// <summary>Army list section (Characters, Core, etc.).</summary>
    public required UnitCategory Category { get; init; }

    /// <summary>True for the book's special characters: one each per army, no magic items of their own.</summary>
    public bool Named { get; init; }

    /// <summary>
    /// The model the entry fields by default: its first priced profile, with the book's own
    /// equipment and special rules. A <see cref="UnitConfiguration"/> may pick another of
    /// <see cref="Profiles"/>, in which case its stats come from there instead.
    /// </summary>
    public required Model BaseModel { get; init; }

    /// <summary>
    /// The priced profiles of the entry — the variants a player chooses between when a book
    /// prints a Captain and a General on one page. Always at least one.
    /// </summary>
    public required IReadOnlyList<ProfileEntry> Profiles { get; init; }

    /// <summary>
    /// Mount model for an entry whose profile block prints one — cavalry, chariots — or null.
    /// A character's mount is chosen instead, from <see cref="MountOptions"/>.
    /// </summary>
    public Model? Mount { get; init; }

    /// <summary>
    /// The mounts a mount option may put the entry on, keyed by the mount's own entry id in the
    /// book — the <c>unitId</c> a mount choice carries. Empty for an entry with no such option.
    /// </summary>
    public IReadOnlyDictionary<string, MountOption> MountOptions { get; init; } =
        new Dictionary<string, MountOption>();

    /// <summary>Base width in mm (parsed from baseSize field).</summary>
    public required int BaseWidthMm { get; init; }

    /// <summary>The entry's option groups, typed, as the bundle prints them.</summary>
    public required IReadOnlyList<OptionGroup> OptionGroups { get; init; }

    /// <summary>Minimum unit size. Characters default to 1.</summary>
    public required int MinUnitSize { get; init; }

    /// <summary>Maximum unit size, if the book states one.</summary>
    public int? MaxUnitSize { get; init; }

    /// <summary>Points cost per model of the default profile, before upgrades.</summary>
    public required double BaseModelCost { get; init; }

    /// <summary>Pre-computed command group availability and costs.</summary>
    public required CommandAvailability Command { get; init; }

    /// <summary>
    /// Full armour lookup table for resolving equipment upgrades.
    /// Set by the repository — contains all armour types, not just this unit's base equipment.
    /// </summary>
    public required IReadOnlyDictionary<ArmourKey, Armour> AvailableArmour { get; init; }

    /// <summary>The raw <see cref="UnitEntry"/> from the bundle.</summary>
    public required UnitEntry RawEntry { get; init; }

    /// <summary>The profile with a given id, or the default when the id names none of them.</summary>
    public ProfileEntry ProfileById(string? profileId) =>
        Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase))
        ?? BaseModel.Stats;

    /// <summary>The magic item budget of the default profile — see <see cref="ResolveMagicItemAllowance"/>.</summary>
    public MagicItemAllowance? MagicItemAllowance => ResolveMagicItemAllowance(BaseModel.Stats.Id, OptionGroups);

    /// <summary>
    /// The magic item budget a profile may spend. An army book prints one options block for a
    /// page of entries, so a Captain's allowance and a General's both land on the entry; the
    /// profile the line names is what separates them. An unnamed allowance belongs to whoever
    /// holds it. When only a neighbour's allowances are present, take the smallest — the entry
    /// can still be equipped, without handing it a budget the book never gave it.
    /// </summary>
    public static MagicItemAllowance? ResolveMagicItemAllowance(string? profileId, IEnumerable<OptionGroup>? groups)
    {
        var allowances = groups?
            .Where(g => g.IsKind(OptionKind.MagicItems))
            .ToList();
        if (allowances is null || allowances.Count == 0)
            return null;

        var mine = allowances.FirstOrDefault(g =>
            g.AppliesTo is not null
            && string.Equals(g.AppliesTo.Trim(), profileId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (mine != null)
            return ToAllowance(mine);

        var unnamed = allowances.FirstOrDefault(g => g.AppliesTo is null);
        if (unnamed != null)
            return ToAllowance(unnamed);

        var smallest = allowances
            .OrderBy(g => g.PointsLimit ?? int.MaxValue)
            .First();
        return ToAllowance(smallest);
    }

    private static MagicItemAllowance ToAllowance(OptionGroup group) => new(
        group.PointsLimit,
        group.ItemTypes,
        group.Families ?? []);
}

/// <summary>
/// One mount an entry may take: the mount's model as its own book entry prints it, and the
/// troop type the rider becomes on it — a character on a War Beast fights as Cavalry, on a
/// Monstrous Beast as Monstrous Cavalry, on a Monster as a ridden Monster.
/// </summary>
public sealed record MountOption(string UnitId, Model Model, TroopTypeInfo RiderTroopType);

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

    /// <summary>
    /// Availability and costs from an entry's command groups. A group whose choice prices
    /// nothing — a champion the book includes for free — is still available at cost 0.
    /// </summary>
    public static CommandAvailability FromGroups(IEnumerable<OptionGroup>? groups)
    {
        if (groups is null) return None;

        bool canChamp = false, canMus = false, canStd = false;
        double champCost = 0, musCost = 0, stdCost = 0;

        foreach (var group in groups)
        {
            if (!group.IsKind(OptionKind.Command)) continue;
            if (!CommandRoleExtensions.TryFromRoleText(group.Role, out var role)) continue;

            double cost = group.Choices.FirstOrDefault()?.Points ?? group.Points ?? 0;
            switch (role)
            {
                case CommandRole.Champion:
                    canChamp = true;
                    champCost = cost;
                    break;
                case CommandRole.Musician:
                    canMus = true;
                    musCost = cost;
                    break;
                case CommandRole.StandardBearer:
                    canStd = true;
                    stdCost = cost;
                    break;
            }
        }

        if (!canChamp && !canMus && !canStd) return None;

        return new CommandAvailability
        {
            CanHaveChampion = canChamp,
            ChampionCost = champCost,
            CanHaveMusician = canMus,
            MusicianCost = musCost,
            CanHaveStandardBearer = canStd,
            StandardBearerCost = stdCost,
        };
    }
}
