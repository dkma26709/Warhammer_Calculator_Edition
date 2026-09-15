using System.Collections.Concurrent;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Loads the bundle and the engine's rules files, resolves all domain objects (weapons,
/// armour, units), caches results. Composes <see cref="CoreRulesService"/> and
/// <see cref="BundleService"/> internally.
/// </summary>
public class GameDataRepository : IGameDataRepository
{
    private readonly CoreRulesService _coreRules;
    private readonly BundleService _bundle;
    private readonly GameSystemService _system;

    private Dictionary<CloseCombatWeaponKey, Weapon>? _weapons;
    private Dictionary<ArmourKey, Armour>? _armour;
    private IReadOnlyList<Faction>? _factions;
    // Concurrent because the test suite shares one repository across parallel test classes;
    // in the app every call lands on the single browser thread.
    private readonly ConcurrentDictionary<string, IReadOnlyList<Unit>> _unitCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CompositionRules> _compositionCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<BaseTroopType, TroopTypeRules>? _troopTypes;

    public GameDataRepository(CoreRulesService coreRules, BundleService bundle, GameSystemService system)
    {
        _coreRules = coreRules;
        _bundle = bundle;
        _system = system;
    }

    // ─── Initialisation ──────────────────────────────────────────

    public async Task EnsureLoadedAsync()
    {
        await _coreRules.EnsureLoadedAsync();
        ResolveWeapons();
        ResolveArmour();
        await ResolveTroopTypesAsync();
        _factions ??= await _bundle.GetFactionsAsync();
    }

    /// <summary>
    /// Load the active system's Troop Types figures. Must run before any unit is resolved —
    /// <see cref="TroopTypeInfo"/> carries no defaults of its own, deliberately, so that a
    /// missing file or row fails here rather than silently handing out another system's rules.
    /// </summary>
    private async Task ResolveTroopTypesAsync()
    {
        if (_troopTypes != null) return;

        var data = await _bundle.GetTroopTypesAsync();
        var resolved = new Dictionary<BaseTroopType, TroopTypeRules>();

        foreach (var type in Enum.GetValues<BaseTroopType>())
        {
            if (!data.Types.TryGetValue(type.ToJsonKey(), out var entry))
                throw new InvalidOperationException(
                    $"troop-types.json for '{_system.CurrentSystem}' is missing '{type.ToJsonKey()}'.");

            resolved[type] = new TroopTypeRules(
                entry.MinRankWidth,
                entry.SupportingAttacks,
                entry.MountSupportingAttacks,
                entry.NonSplitSupportingAttacks,
                entry.ImpactHits,
                entry.UnitStrength,
                entry.SplitProfile,
                entry.Swiftstride);
        }

        _troopTypes = resolved;
    }

    /// <summary>The active system's figures for a troop type.</summary>
    public TroopTypeRules GetTroopTypeRules(BaseTroopType type) =>
        (_troopTypes ?? throw new InvalidOperationException(
            "Troop type figures not loaded — call EnsureLoadedAsync first."))[type];

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

    // Empty until EnsureLoadedAsync has read the bundle's faction list (components may render
    // before initialisation).
    public IReadOnlyList<Faction> AllFactions => _factions ?? [];

    public string GetFactionDisplayName(string factionId) =>
        AllFactions.FirstOrDefault(f => string.Equals(f.Id, factionId, StringComparison.OrdinalIgnoreCase))?.Name
        ?? factionId;

    public Task<FactionData> GetFactionAsync(string factionId) => _bundle.GetFactionAsync(factionId);

    public async Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync()
    {
        var bundle = await _bundle.GetBundleAsync();
        return bundle.Rulebook.Items;
    }

    public async Task<IReadOnlyList<LoreEntry>> GetLoresAsync(string factionId)
    {
        var faction = await _bundle.GetFactionAsync(factionId);
        var bundle = await _bundle.GetBundleAsync();

        var lores = new List<LoreEntry>(faction.Lores);
        foreach (var lore in bundle.Rulebook.Lores)
        {
            if (!lores.Any(l => string.Equals(l.Name, lore.Name, StringComparison.OrdinalIgnoreCase)))
                lores.Add(lore);
        }

        return lores;
    }

    // ─── Units ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<Unit>> GetUnitsAsync(string factionId)
    {
        if (_unitCache.TryGetValue(factionId, out var cached))
            return cached;

        await EnsureLoadedAsync();

        var faction = await _bundle.GetFactionAsync(factionId);
        var byId = new Dictionary<string, UnitEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in faction.Units)
            byId.TryAdd(entry.Id, entry);

        var units = new List<Unit>(faction.Units.Count);
        foreach (var entry in faction.Units)
        {
            var unit = ResolveUnit(entry, faction.Id, byId);
            if (unit != null)
                units.Add(unit);
        }

        _unitCache[factionId] = units;
        return units;
    }

    // ─── Combat tables ───────────────────────────────────────────

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

    public RollRequirement GetToHitRequirement(int attackerWS, int defenderWS) =>
        _coreRules.GetToHitRequirement(attackerWS, defenderWS);

    public RollRequirement GetToWoundRequirement(int strength, int toughness) =>
        _coreRules.GetToWoundRequirement(strength, toughness);

    public int GetArmourSaveModifier(int strength) => _coreRules.GetArmourSaveModifier(strength);

    // ─── Composition ─────────────────────────────────────────────

    public async Task<CompositionRules> GetCompositionRulesAsync(string? factionId = null)
    {
        var cacheKey = factionId ?? "";
        if (_compositionCache.TryGetValue(cacheKey, out var cached)) return cached;

        var bundle = await _bundle.GetBundleAsync();
        var system = bundle.Composition
            ?? throw new InvalidOperationException(
                $"The bundle for '{_system.CurrentSystem}' carries no composition block.");

        CompositionData? own = null;
        if (factionId != null)
            own = (await _bundle.GetFactionAsync(factionId)).Composition;

        var rules = CompositionRules.FromData(system, own, _system.CurrentSystem);
        _compositionCache[cacheKey] = rules;
        return rules;
    }

    // ─── Unit Resolution ─────────────────────────────────────────

    private Unit? ResolveUnit(UnitEntry entry, string factionId, IReadOnlyDictionary<string, UnitEntry> byId)
    {
        // A chapter outside the army list — a bestiary, an appendix — has no section, and the
        // book's mounts section is kept for what the mount options point at.
        if (UnitCategoryExtensions.FromSection(entry.Section, entry.Tier, entry.Named) is not { } category)
            return null;
        if (entry.Profiles.Count == 0)
            return null;

        var (priced, mountProfile) = UnitConfiguration.ResolveProfiles(entry);
        var primaryProfile = priced[0];
        var troopType = TroopTypeInfo.Parse(entry.TroopType, GetTroopTypeRules);

        // Several priced profiles on a character entry are variants — a Captain or a General.
        // On a unit they are the rank and file and a handler priced apart, and the handler is
        // not something the unit is fielded as.
        if (!troopType.IsCharacter && priced.Count > 1)
            priced = [primaryProfile];
        var parsed = ParsedSpecialRules.ParseScoped(entry.SpecialRules);

        // A rule the book scopes to one model — "Mighty Blow (2) (War Boars only)" — belongs to
        // whichever profile that name picks out. Only two profiles are modelled as fighters, the
        // rider and its mount, so a scope naming a handler, a chariot's beasts or a third crew
        // profile lands nowhere and is deliberately not applied: the engine fields no model for it
        // to arm. Applying such a rule unit-wide is what used to happen, and it armed every model
        // in the unit with a single champion's or mount's rule.
        bool mountFights = mountProfile != null && troopType.HasSplitProfile;

        // Match against EVERY profile, not just the two that fight. A unit of Slaves lists
        // "Murderous Prowess (Slave Master only)" and has profiles Slave and Slave Master; offered
        // only the Slave, the scope half-matches it on the shared word and arms all twenty slaves
        // with their champion's rule. Offered both, it picks the Slave Master — a profile the
        // engine fields no fighter for, so the rule correctly goes nowhere.
        var profileNames = entry.Profiles.Select(p => p.Name).ToList();
        var specialRules = parsed.UnitWide;
        var mountRules = new ParsedSpecialRules();
        foreach (var scoped in parsed.Scoped)
        {
            var index = ModelScope.BestMatch(scoped.Scope, profileNames);
            if (index is null)
                continue;

            var named = profileNames[index.Value];
            if (named == primaryProfile.Name)
                specialRules = specialRules.Merge(scoped.Rules);
            else if (mountFights && named == mountProfile!.Name)
                mountRules = mountRules.Merge(scoped.Rules);
        }

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
        if (mountFights)
        {
            mount = new Model
            {
                Name = mountProfile!.Name,
                Stats = mountProfile,
                TroopType = troopType,
                SpecialRules = mountRules,
                Weapons = [],
                Armour = [],
            };
        }

        var mountOptions = ResolveMountOptions(entry, byId);

        return new Unit
        {
            Id = entry.Id,
            Name = entry.Name,
            FactionId = factionId,
            Category = category,
            Named = entry.Named,
            BaseModel = baseModel,
            Profiles = priced,
            Mount = mount,
            MountOptions = mountOptions,
            BaseWidthMm = UnitConfiguration.ParseBaseWidthMm(entry.BaseSize),
            OptionGroups = entry.OptionGroups,
            MinUnitSize = troopType.IsCharacter
                ? 1
                : entry.UnitSizeParsed?.Min ?? UnitConfiguration.ParseMinUnitSize(entry.UnitSize),
            MaxUnitSize = entry.UnitSizeParsed?.Max ?? ParseMaxUnitSize(entry.UnitSize),
            BaseModelCost = primaryProfile.Points ?? 0,
            Command = CommandAvailability.FromGroups(entry.OptionGroups),
            AvailableArmour = _armour ?? new Dictionary<ArmourKey, Armour>(),
            RawEntry = entry,
        };
    }

    /// <summary>
    /// The mounts an entry's mount option points at, as the book prints them in its mounts
    /// section: the mount's own profile and rules, and the troop type the rider becomes on it.
    /// A choice whose id names no entry in the book — a mount the book only describes in prose —
    /// is left out, and the choice still costs its points and prints its name.
    /// </summary>
    private Dictionary<string, MountOption> ResolveMountOptions(UnitEntry entry, IReadOnlyDictionary<string, UnitEntry> byId)
    {
        var mounts = new Dictionary<string, MountOption>(StringComparer.OrdinalIgnoreCase);

        foreach (var (group, _) in entry.OptionGroups.Flatten())
        {
            if (!group.IsKind(OptionKind.Mount)) continue;

            foreach (var choice in group.Choices)
            {
                if (choice.UnitId is null || mounts.ContainsKey(choice.UnitId)) continue;
                if (!byId.TryGetValue(choice.UnitId, out var mountEntry) || mountEntry.Profiles.Count == 0) continue;

                var mountType = TroopTypeInfo.Parse(mountEntry.TroopType, GetTroopTypeRules);
                var riderType = TroopTypeInfo.Parse(RiderTypeOn(mountType.BaseType) + " (Character)", GetTroopTypeRules);

                var model = new Model
                {
                    Name = mountEntry.Profiles[0].Name is { Length: > 0 } name ? name : mountEntry.Name,
                    Stats = mountEntry.Profiles[0],
                    TroopType = riderType,
                    SpecialRules = ParsedSpecialRules.Parse(mountEntry.SpecialRules),
                    Weapons = [],
                    Armour = [],
                };

                mounts[choice.UnitId] = new MountOption(choice.UnitId, model, riderType);
            }
        }

        return mounts;
    }

    /// <summary>
    /// The troop type a character fights as on a mount, per the Troop Types chapter: on a War
    /// Beast the model uses the rules for Cavalry, on a Monstrous Beast those for Monstrous
    /// Cavalry, and on a Monster, Chariot or Shrine it takes that type whole.
    /// </summary>
    private static string RiderTypeOn(BaseTroopType mountType) => mountType switch
    {
        BaseTroopType.WarBeasts => "Cavalry",
        BaseTroopType.MonstrousBeasts => "Monstrous Cavalry",
        BaseTroopType.Cavalry => "Cavalry",
        BaseTroopType.MonstrousCavalry => "Monstrous Cavalry",
        BaseTroopType.Monster => "Monster",
        BaseTroopType.Chariot => "Chariot",
        BaseTroopType.Shrine => "Shrine",
        BaseTroopType.MonstrousInfantry => "Monstrous Cavalry",
        _ => "Cavalry",
    };

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

    private static int? ParseMaxUnitSize(string? unitSize)
    {
        if (string.IsNullOrWhiteSpace(unitSize)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(unitSize, @"(\d+)\s*[-–]\s*(\d+)");
        return match.Success ? int.Parse(match.Groups[2].Value) : null;
    }
}
