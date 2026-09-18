using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Central data access layer. Loads the bundle and the engine's rules files, resolves all
/// domain objects, caches the result. Consumers get fully-hydrated domain models — no raw
/// JSON records, no string lookups — except the bundle's own reference lists (items, lores,
/// upgrades), which are data the pages present as they are.
/// </summary>
public interface IGameDataRepository
{
    /// <summary>Ensure core rules, weapon/armour definitions and the faction list are loaded.</summary>
    Task EnsureLoadedAsync();

    // ─── Factions ────────────────────────────────────────────────

    /// <summary>All army books in the active system, in the bundle's order. Empty before <see cref="EnsureLoadedAsync"/>.</summary>
    IReadOnlyList<Faction> AllFactions { get; }

    /// <summary>Display name for a faction id (e.g. "Dark Elves"), or the id itself when unknown.</summary>
    string GetFactionDisplayName(string factionId);

    // ─── Units ───────────────────────────────────────────────────

    /// <summary>Load and resolve all units for a faction. Cached after first call.</summary>
    Task<IReadOnlyList<Unit>> GetUnitsAsync(string factionId);

    /// <summary>The faction's own book data: army rules, magic items, upgrade chapters, lores.</summary>
    Task<FactionData> GetFactionAsync(string factionId);

    /// <summary>The rulebook's common magic items, available to every faction.</summary>
    Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync();

    /// <summary>The lores a faction's wizards may use: the book's own, then the rulebook's.</summary>
    Task<IReadOnlyList<LoreEntry>> GetLoresAsync(string factionId);

    /// <summary>
    /// The active system's army composition rules, with the faction's own unit constraints
    /// when a faction is given.
    /// </summary>
    Task<CompositionRules> GetCompositionRulesAsync(string? factionId = null);

    // ─── Weapons & Armour ────────────────────────────────────────

    /// <summary>Get a resolved close combat weapon by key.</summary>
    Weapon? GetWeapon(CloseCombatWeaponKey key);

    /// <summary>Get the hand weapon (always available as fallback).</summary>
    Weapon GetHandWeapon();

    /// <summary>Get a resolved armour piece by key.</summary>
    Armour? GetArmour(ArmourKey key);

    // ─── Combat Tables ───────────────────────────────────────────

    /// <summary>What a close combat attack must roll to hit.</summary>
    RollRequirement GetToHitRequirement(int attackerWS, int defenderWS);

    /// <summary>What a hit must roll to wound — may need a confirming second D6.</summary>
    RollRequirement GetToWoundRequirement(int strength, int toughness);

    /// <summary>Armour save modifier for a given strength (negative value).</summary>
    int GetArmourSaveModifier(int strength);
}
