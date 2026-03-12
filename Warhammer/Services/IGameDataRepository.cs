using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Central data access layer. Loads JSON, resolves all domain objects, caches the result.
/// Consumers get fully-hydrated domain models — no raw JSON records, no string lookups.
/// </summary>
public interface IGameDataRepository
{
    /// <summary>Ensure core rules and all weapon/armour definitions are loaded.</summary>
    Task EnsureLoadedAsync();

    // ─── Factions ────────────────────────────────────────────────

    /// <summary>All available factions.</summary>
    IReadOnlyList<FactionKey> AllFactions { get; }

    /// <summary>Display name for a faction (e.g., "Dark Elves").</summary>
    string GetFactionDisplayName(FactionKey faction);

    // ─── Units ───────────────────────────────────────────────────

    /// <summary>Load and resolve all units for a faction. Cached after first call.</summary>
    Task<IReadOnlyList<Unit>> GetUnitsAsync(FactionKey faction);

    /// <summary>Load faction extras (army rules, magic items, lores, upgrades).</summary>
    Task<FactionExtrasData> GetFactionExtrasAsync(FactionKey faction);

    // ─── Weapons & Armour ────────────────────────────────────────

    /// <summary>Get a resolved close combat weapon by key.</summary>
    Weapon? GetWeapon(CloseCombatWeaponKey key);

    /// <summary>Get the hand weapon (always available as fallback).</summary>
    Weapon GetHandWeapon();

    /// <summary>Get a resolved armour piece by key.</summary>
    Armour? GetArmour(ArmourKey key);

    // ─── Combat Tables ───────────────────────────────────────────

    /// <summary>D6 score needed to hit in close combat.</summary>
    int GetToHitScore(int attackerWS, int defenderWS);

    /// <summary>D6 score needed to wound.</summary>
    int GetToWoundScore(int strength, int toughness);

    /// <summary>Armour save modifier for a given strength (negative value).</summary>
    int GetArmourSaveModifier(int strength);
}
