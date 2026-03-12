using Warhammer.Models.Json;

namespace Warhammer.Models.Domain;

/// <summary>
/// A single figure on the tabletop — stats, equipment, and special rules.
/// Immutable template created during data loading.
/// Examples: a Swordsman, a Prince, a Warhorse.
/// </summary>
public sealed record Model
{
    /// <summary>Display name (e.g., "Prince", "Elven Steed").</summary>
    public required string Name { get; init; }

    /// <summary>Raw stat profile: M/WS/BS/S/T/W/I/A/Ld.</summary>
    public required ProfileEntry Stats { get; init; }

    /// <summary>Parsed troop type with flags (character, split profile, etc.).</summary>
    public required TroopTypeInfo TroopType { get; init; }

    /// <summary>Parsed special rules — 60+ strongly-typed boolean/int fields.</summary>
    public required ParsedSpecialRules SpecialRules { get; init; }

    /// <summary>Pre-resolved close combat weapons available to this model.</summary>
    public required IReadOnlyList<Weapon> Weapons { get; init; }

    /// <summary>Pre-resolved armour equipped by this model.</summary>
    public required IReadOnlyList<Armour> Armour { get; init; }
}
