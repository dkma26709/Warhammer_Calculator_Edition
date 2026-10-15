using System.Net.Http.Json;
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
    private readonly GameSystemService _system;

    private Dictionary<CloseCombatWeaponKey, Weapon>? _weapons;
    private Dictionary<ArmourKey, Armour>? _armour;
    private IReadOnlyList<FactionKey>? _factions;
    private readonly Dictionary<FactionKey, IReadOnlyList<Unit>> _unitCache = new();
    private IReadOnlyList<MagicItemEntry>? _commonMagicItems;
    private CompositionRules? _composition;
    private Dictionary<BaseTroopType, TroopTypeRules>? _troopTypes;

    public GameDataRepository(CoreRulesService coreRules, FactionDataService factionData, GameSystemService system)
    {
        _coreRules = coreRules;
        _factionData = factionData;
        _system = system;
    }

    // ─── Initialisation ──────────────────────────────────────────

    public async Task EnsureLoadedAsync()
    {
        await _coreRules.EnsureLoadedAsync();
        ResolveWeapons();
        ResolveArmour();
        await ResolveTroopTypesAsync();
        await ResolveFactionsAsync();
    }

    /// <summary>
    /// Load the active system's Troop Types figures. Must run before any unit is resolved —
    /// <see cref="TroopTypeInfo"/> carries no defaults of its own, deliberately, so that a
    /// missing file or row fails here rather than silently handing out another system's rules.
    /// </summary>
    private async Task ResolveTroopTypesAsync()
    {
        if (_troopTypes != null) return;

        var data = await _factionData.GetTroopTypesAsync();
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

    private async Task ResolveFactionsAsync()
    {
        if (_factions != null) return;

        var manifest = await _factionData.GetManifestAsync();
        var factions = new List<FactionKey>(manifest.Factions.Count);
        foreach (var jsonKey in manifest.Factions)
        {
            if (FactionKeyExtensions.TryFromJsonKey(jsonKey, out var key))
                factions.Add(key);
            else
                Console.Error.WriteLine($"Manifest faction '{jsonKey}' has no FactionKey — skipped.");
        }

        _factions = factions;
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

    // Empty until EnsureLoadedAsync resolves the manifest, since components may render before
    // initialisation; the enum spans every system, so it is not a usable fallback.
    public IReadOnlyList<FactionKey> AllFactions => _factions ?? [];

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

    public async Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync()
    {
        if (_commonMagicItems != null)
            return _commonMagicItems;

        var http = _factionData.GetHttpClient();
        var items = await http.GetFromJsonAsync<List<MagicItemEntry>>(
            _system.DataPath("common-magic-items.json"),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        _commonMagicItems = items ?? [];
        return _commonMagicItems;
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

    public RollRequirement GetToHitRequirement(int attackerWS, int defenderWS) =>
        _coreRules.GetToHitRequirement(attackerWS, defenderWS);

    public RollRequirement GetToWoundRequirement(int strength, int toughness) =>
        _coreRules.GetToWoundRequirement(strength, toughness);
    public int GetArmourSaveModifier(int strength) => _coreRules.GetArmourSaveModifier(strength);

    // ─── Composition ─────────────────────────────────────────────

    public async Task<CompositionRules> GetCompositionRulesAsync()
    {
        if (_composition != null) return _composition;

        var data = await _factionData.GetCompositionAsync();

        // A category absent from the file has no limit of that kind. Core/Special/Rare are
        // required because every system so far constrains them; Lords/Heroes are not, since
        // wap-v3 merges them into one Characters section.
        double? Max(string category) =>
            data.Categories.TryGetValue(category, out var entry) ? entry.MaxPercent : null;
        double? Min(string category) =>
            data.Categories.TryGetValue(category, out var entry) ? entry.MinPercent : null;

        double Require(double? value, string what) => value
            ?? throw new InvalidOperationException(
                $"composition.json for '{_system.CurrentSystem}' is missing {what}.");

        _composition = new CompositionRules
        {
            LordsMaxPercent = Max("lords"),
            HeroesMaxPercent = Max("heroes"),
            CharactersCombinedMaxPercent = data.CharactersCombinedMaxPercent,
            SingleUnitMaxPercent = data.SingleUnitMaxPercent,
            CoreMinPercent = Require(Min("core"), "categories.core.minPercent"),
            SpecialMaxPercent = Require(Max("special"), "categories.special.maxPercent"),
            RareMaxPercent = Require(Max("rare"), "categories.rare.maxPercent"),
        };
        return _composition;
    }

    // ─── Unit Resolution ─────────────────────────────────────────

    private Unit? ResolveUnit(UnitEntry entry, FactionKey faction)
    {
        if (!UnitCategoryExtensions.TryFromJsonKey(entry.Category, out var category))
            return null;

        var (primaryProfile, mountProfile) = UnitConfiguration.ResolveProfiles(entry);
        var troopType = TroopTypeInfo.Parse(entry.TroopType, GetTroopTypeRules);
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
        var profileNames = entry.Profiles?.Select(p => p.Name).ToList() ?? [];
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

        var command = ResolveCommandAvailability(entry.Options);
        var magicItemAllowance = Unit.ResolveMagicItemAllowance(entry.Name, entry.Options);

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
            AvailableArmour = _armour ?? new Dictionary<ArmourKey, Armour>(),
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
