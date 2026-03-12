using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Loads JSON data, resolves all domain objects (weapons, armour, units), caches results.
/// Composes <see cref="CoreRulesService"/> and <see cref="FactionDataService"/> internally.
/// </summary>
public class GameDataRepository : IGameDataRepository
{
    private readonly CoreRulesService _coreRules;
    private readonly FactionDataService _factionData;

    private Dictionary<CloseCombatWeaponKey, Weapon>? _weapons;
    private Dictionary<ArmourKey, Armour>? _armour;
    private readonly Dictionary<FactionKey, IReadOnlyList<Unit>> _unitCache = new();

    public GameDataRepository(CoreRulesService coreRules, FactionDataService factionData)
    {
        _coreRules = coreRules;
        _factionData = factionData;
    }

    // ─── Initialisation ──────────────────────────────────────────

    public async Task EnsureLoadedAsync()
    {
        await _coreRules.EnsureLoadedAsync();
        ResolveWeapons();
        ResolveArmour();
    }

    private void ResolveWeapons()
    {
        if (_weapons != null) return;
        _weapons = new Dictionary<CloseCombatWeaponKey, Weapon>();

        foreach (var (jsonKey, entry) in _coreRules.Data.Weapons.CloseCombat)
        {
            if (CloseCombatWeaponKeyExtensions.TryFromJsonKey(jsonKey, out var key))
            {
                _weapons[key] = new Weapon
                {
                    Key = key,
                    Name = entry.Name,
                    Strength = WeaponStrength.Parse(entry.Strength),
                    Rules = ParsedWeaponRules.Parse(entry.SpecialRules, entry.Notes),
                };
            }
        }
    }

    private void ResolveArmour()
    {
        if (_armour != null) return;
        _armour = new Dictionary<ArmourKey, Armour>();

        foreach (var (jsonKey, entry) in _coreRules.Data.Armour)
        {
            if (ArmourKeyExtensions.TryFromJsonKey(jsonKey, out var key))
            {
                var (combatMod, combatSave) = Models.Domain.Armour.ParseSaveField(entry.CombatSave);
                var (missileMod, missileSave) = Models.Domain.Armour.ParseSaveField(entry.MissileSave);

                _armour[key] = new Armour
                {
                    Key = key,
                    Name = entry.Name,
                    CombatSaveModifier = combatMod,
                    CombatStandaloneSave = combatSave,
                    MissileSaveModifier = missileMod,
                    MissileStandaloneSave = missileSave,
                    ParryValue = Models.Domain.Armour.ParseParryValue(entry.SpecialRules),
                };
            }
        }
    }

    // ─── Factions ────────────────────────────────────────────────

    public IReadOnlyList<FactionKey> AllFactions => FactionKeyExtensions.All;

    public string GetFactionDisplayName(FactionKey faction) => faction.ToDisplayName();

    // ─── Units ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<Unit>> GetUnitsAsync(FactionKey faction)
    {
        if (_unitCache.TryGetValue(faction, out var cached))
            return cached;

        await EnsureLoadedAsync();

        var factionData = await _factionData.GetFactionUnitsAsync(faction.ToJsonKey());
        var units = new List<Unit>(factionData.Units.Count);

        foreach (var entry in factionData.Units)
        {
            var unit = ResolveUnit(entry, faction);
            if (unit != null)
                units.Add(unit);
        }

        _unitCache[faction] = units;
        return units;
    }

    public async Task<FactionExtrasData> GetFactionExtrasAsync(FactionKey faction)
    {
        return await _factionData.GetFactionExtrasAsync(faction.ToJsonKey());
    }

    // ─── Weapons & Armour ────────────────────────────────────────

    public Weapon? GetWeapon(CloseCombatWeaponKey key)
    {
        return _weapons?.TryGetValue(key, out var w) == true ? w : null;
    }

    public Weapon GetHandWeapon()
    {
        return _weapons?[CloseCombatWeaponKey.HandWeapon]
            ?? throw new InvalidOperationException("Core rules not loaded. Call EnsureLoadedAsync first.");
    }

    public Armour? GetArmour(ArmourKey key)
    {
        return _armour?.TryGetValue(key, out var a) == true ? a : null;
    }

    // ─── Combat Tables ───────────────────────────────────────────

    public int GetToHitScore(int attackerWS, int defenderWS) => _coreRules.GetToHitScore(attackerWS, defenderWS);
    public int GetToWoundScore(int strength, int toughness) => _coreRules.GetToWoundScore(strength, toughness);
    public int GetArmourSaveModifier(int strength) => _coreRules.GetArmourSaveModifier(strength);

    // ─── Unit Resolution ─────────────────────────────────────────

    private Unit? ResolveUnit(UnitEntry entry, FactionKey faction)
    {
        if (!UnitCategoryExtensions.TryFromJsonKey(entry.Category, out var category))
            return null;

        var (primaryProfile, mountProfile) = UnitConfiguration.ResolveProfiles(entry);
        var troopType = TroopTypeInfo.Parse(entry.TroopType);
        var specialRules = ParsedSpecialRules.Parse(entry.SpecialRules);

        var (weaponKeys, armourKeys) = ResolveEquipmentKeys(entry.Equipment);
        var weapons = ResolveWeaponList(weaponKeys);
        var armour = ResolveArmourList(armourKeys);

        var baseModel = new Model
        {
            Name = primaryProfile.Name,
            Stats = primaryProfile,
            TroopType = troopType,
            SpecialRules = specialRules,
            Weapons = weapons,
            Armour = armour,
        };

        Model? mount = null;
        if (mountProfile != null && troopType.HasSplitProfile)
        {
            mount = new Model
            {
                Name = mountProfile.Name,
                Stats = mountProfile,
                TroopType = troopType,
                SpecialRules = specialRules,
                Weapons = [],
                Armour = [],
            };
        }

        var command = ResolveCommandAvailability(entry.Options);
        var magicItemAllowance = entry.Options?
            .Where(o => o.Type == "magic_items")
            .Select(o => o.PointsLimit)
            .FirstOrDefault();

        return new Unit
        {
            Name = entry.Name,
            Faction = faction,
            Category = category,
            BaseModel = baseModel,
            Mount = mount,
            BaseWidthMm = UnitConfiguration.ParseBaseWidthMm(entry.BaseSize),
            Options = entry.Options ?? [],
            MinUnitSize = troopType.IsCharacter ? 1 : UnitConfiguration.ParseMinUnitSize(entry.UnitSize),
            MaxUnitSize = ParseMaxUnitSize(entry.UnitSize),
            BaseModelCost = entry.PointsCost ?? primaryProfile.Points ?? 0,
            Command = command,
            MagicItemAllowance = magicItemAllowance,
            RawEntry = entry,
        };
    }

    private (List<CloseCombatWeaponKey>, List<ArmourKey>) ResolveEquipmentKeys(string? equipment)
    {
        var (weaponStringKeys, armourStringKeys) = EquipmentParser.Parse(equipment);

        var weaponKeys = new List<CloseCombatWeaponKey>();
        foreach (var key in weaponStringKeys)
        {
            if (CloseCombatWeaponKeyExtensions.TryFromJsonKey(key, out var weaponKey))
                weaponKeys.Add(weaponKey);
        }

        var armourKeys = new List<ArmourKey>();
        foreach (var key in armourStringKeys)
        {
            if (ArmourKeyExtensions.TryFromJsonKey(key, out var armourKey))
                armourKeys.Add(armourKey);
        }

        return (weaponKeys, armourKeys);
    }

    private List<Weapon> ResolveWeaponList(List<CloseCombatWeaponKey> keys)
    {
        var result = new List<Weapon>(keys.Count);
        foreach (var key in keys)
        {
            if (_weapons?.TryGetValue(key, out var weapon) == true)
                result.Add(weapon);
        }

        return result;
    }

    private List<Armour> ResolveArmourList(List<ArmourKey> keys)
    {
        var result = new List<Armour>(keys.Count);
        foreach (var key in keys)
        {
            if (_armour?.TryGetValue(key, out var armour) == true)
                result.Add(armour);
        }

        return result;
    }

    private static CommandAvailability ResolveCommandAvailability(List<UnitOption>? options)
    {
        if (options == null || options.Count == 0)
            return CommandAvailability.None;

        bool canChamp = false, canMus = false, canStd = false;
        double champCost = 0, musCost = 0, stdCost = 0;

        foreach (var opt in options)
        {
            if (opt.Type != "command" || opt.Role == null) continue;
            double cost = opt.Points ?? 0;

            if (CommandRoleExtensions.TryFromRoleText(opt.Role, out var role))
            {
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
        }

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

    private static int? ParseMaxUnitSize(string? unitSize)
    {
        if (string.IsNullOrWhiteSpace(unitSize)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(unitSize, @"(\d+)\s*[-–]\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[2].Value) : null;
    }
}
