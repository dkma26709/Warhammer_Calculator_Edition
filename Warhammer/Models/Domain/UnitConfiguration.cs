using System.Text.RegularExpressions;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Models.Domain;

public class UnitConfiguration
{
    // ─── Source data ─────────────────────────────────────────────

    /// <summary>
    /// The fully-resolved unit template. Null when constructed from raw UnitEntry (legacy path).
    /// </summary>
    public Unit? ResolvedUnit { get; }

    /// <summary>
    /// The raw JSON unit entry. Available on both construction paths.
    /// </summary>
    public UnitEntry UnitEntry { get; }

    /// <summary>Kept for backward compatibility. Prefer <see cref="UnitEntry"/>.</summary>
    public UnitEntry Unit => UnitEntry;

    public ProfileEntry PrimaryProfile { get; }
    public ProfileEntry? MountProfile { get; }

    // ─── User configuration ─────────────────────────────────────

    public int ModelCount { get; set; }
    public int Frontage { get; set; }

    /// <summary>Selected weapon as enum. Preferred over <see cref="SelectedWeaponKey"/>.</summary>
    public CloseCombatWeaponKey SelectedWeapon { get; set; }

    /// <summary>Selected weapon as string key (legacy). Kept in sync with <see cref="SelectedWeapon"/>.</summary>
    public string SelectedWeaponKey
    {
        get => SelectedWeapon.ToJsonKey();
        set
        {
            if (CloseCombatWeaponKeyExtensions.TryFromJsonKey(value, out var key))
                SelectedWeapon = key;
        }
    }

    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }
    public ShadowDance SelectedShadowDance { get; set; }
    public TreeAspect SelectedTreeAspect { get; set; }
    public bool UseTreeWhack { get; set; }

    /// <summary>
    /// Tracks selected equipment upgrades. Key = index into Unit.Options,
    /// Value = index into that option's Choices list. Absent or -1 = none selected.
    /// </summary>
    public Dictionary<int, int> SelectedUpgrades { get; } = new();

    // ─── Parsed base data ────────────────────────────────────────

    public TroopTypeInfo TroopType { get; }
    public int BaseWidthMm { get; }
    public ParsedSpecialRules SpecialRules { get; }
    public List<string> BaseWeaponKeys { get; }
    public List<string> BaseArmourKeys { get; }

    // ─── Constructors ────────────────────────────────────────────

    /// <summary>
    /// Construct from a fully-resolved <see cref="Domain.Unit"/>. Preferred path.
    /// No parsing happens here — all data is pre-resolved by the repository.
    /// </summary>
    public UnitConfiguration(Unit unit)
    {
        ResolvedUnit = unit;
        UnitEntry = unit.RawEntry;
        PrimaryProfile = unit.BaseModel.Stats;
        MountProfile = unit.Mount?.Stats;
        TroopType = unit.BaseModel.TroopType;
        BaseWidthMm = unit.BaseWidthMm;
        SpecialRules = unit.BaseModel.SpecialRules;
        BaseWeaponKeys = unit.BaseModel.Weapons.Select(w => w.Key.ToJsonKey()).ToList();
        BaseArmourKeys = unit.BaseModel.Armour.Select(a => a.Key.ToJsonKey()).ToList();

        // Defaults
        ModelCount = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.MinUnitSize;
        Frontage = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.BaseModel.TroopType.MinRankWidth;
        SelectedWeapon = unit.BaseModel.Weapons
            .Select(w => w.Key)
            .FirstOrDefault(CloseCombatWeaponKey.HandWeapon);
    }

    /// <summary>
    /// Construct from raw JSON <see cref="UnitEntry"/>. Legacy path — parses on construction.
    /// </summary>
    public UnitConfiguration(UnitEntry unit)
    {
        ResolvedUnit = null;
        UnitEntry = unit;
        (PrimaryProfile, MountProfile) = ResolveProfiles(unit);
        TroopType = TroopTypeInfo.Parse(unit.TroopType);
        BaseWidthMm = ParseBaseWidthMm(unit.BaseSize);
        SpecialRules = ParsedSpecialRules.Parse(unit.SpecialRules);
        (BaseWeaponKeys, BaseArmourKeys) = EquipmentParser.Parse(unit.Equipment);

        // Defaults
        ModelCount = TroopType.IsCharacter ? 1 : ParseMinUnitSize(unit.UnitSize);
        Frontage = TroopType.IsCharacter ? 1 : TroopType.MinRankWidth;
        SelectedWeaponKey = BaseWeaponKeys.FirstOrDefault() ?? "handWeapon";
    }

    // ─── Resolved equipment access ───────────────────────────────

    /// <summary>
    /// Get the selected weapon as a resolved <see cref="Weapon"/> domain object.
    /// Only available when constructed from <see cref="Domain.Unit"/>.
    /// </summary>
    public Weapon? GetSelectedResolvedWeapon()
    {
        if (ResolvedUnit == null) return null;
        return ResolvedUnit.BaseModel.Weapons
            .FirstOrDefault(w => w.Key == SelectedWeapon);
    }

    /// <summary>
    /// Pre-compute base armour modifier from resolved <see cref="Armour"/> objects.
    /// Only available when constructed from <see cref="Domain.Unit"/>. No lambda needed.
    /// </summary>
    public int ComputeResolvedBaseArmourModifier()
    {
        if (ResolvedUnit == null)
            throw new InvalidOperationException("Cannot compute resolved armour without a resolved Unit. Use ComputeBaseArmourModifier(lookup) instead.");

        int totalModifier = 0;

        // Base armour from resolved objects
        foreach (var armour in ResolvedUnit.BaseModel.Armour)
            totalModifier += armour.CombatSaveModifier;

        // TODO: apply upgrade armour from SelectedUpgrades once
        // the upgrade resolution is migrated to use resolved Armour objects.

        if (SpecialRules.NaturalArmour.HasValue)
            totalModifier += 7 - SpecialRules.NaturalArmour.Value;

        if (TroopType.IntrinsicArmourSave.HasValue)
            totalModifier += 7 - TroopType.IntrinsicArmourSave.Value;

        if (SpecialRules.ChaosArmour)
        {
            bool hasConventionalArmour = ResolvedUnit.BaseModel.Armour
                .Any(a => a.Key is ArmourKey.LightArmour or ArmourKey.MediumArmour or ArmourKey.HeavyArmour);
            if (hasConventionalArmour)
                totalModifier += 1;
        }

        if (SpecialRules.GromrilArmour)
        {
            bool hasConventionalArmour = ResolvedUnit.BaseModel.Armour
                .Any(a => a.Key is ArmourKey.LightArmour or ArmourKey.MediumArmour or ArmourKey.HeavyArmour);
            if (hasConventionalArmour)
                totalModifier += 1;
        }

        return totalModifier;
    }

    // ─── Effective Equipment (base + upgrades - replacements) ────

    /// <summary>
    /// All weapon keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<string> EquipmentWeapons
    {
        get
        {
            var weapons = new List<string>(BaseWeaponKeys);
            ApplySelectedUpgrades(weapons, isWeapon: true);
            return weapons;
        }
    }

    /// <summary>
    /// All armour keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<string> EquipmentArmour
    {
        get
        {
            var armour = new List<string>(BaseArmourKeys);
            ApplySelectedUpgrades(armour, isWeapon: false);
            return armour;
        }
    }

    private void ApplySelectedUpgrades(List<string> keys, bool isWeapon)
    {
        if (UnitEntry.Options == null) return;

        for (int i = 0; i < UnitEntry.Options.Count; i++)
        {
            var option = UnitEntry.Options[i];
            if (option.Type != "equipment" || option.Choices == null) continue;
            if (!SelectedUpgrades.TryGetValue(i, out int choiceIdx) || choiceIdx < 0) continue;
            if (choiceIdx >= option.Choices.Count) continue;

            var choice = option.Choices[choiceIdx];

            // Handle replace: remove the replaced item
            string? replaces = choice.Replaces ?? option.Replaces;
            if (option.Action == "replace" && replaces != null)
            {
                if (EquipmentParser.TryNormalize(replaces, out var replacedKey, out bool replacedIsWeapon)
                    && replacedIsWeapon == isWeapon)
                {
                    keys.Remove(replacedKey);
                }
            }

            // Add the selected items
            foreach (var name in choice.Name)
            {
                if (EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon)
                    && itemIsWeapon == isWeapon && !keys.Contains(key))
                {
                    keys.Add(key);
                }
            }
        }
    }

    // ─── Combat Stats ────────────────────────────────────────────

    /// <summary>
    /// Calculate effective strength for a given weapon.
    /// Weapon Mighty Blow and unit Mighty Blow use the highest value (don't stack).
    /// </summary>
    public int EffectiveStrength(WeaponEntry? weapon, bool isCharging, int combatRound,
        int weaponMightyBlow = 0)
    {
        int baseS = PrimaryProfile.S ?? 3;
        int s = baseS;

        if (weapon != null)
            s = CoreRulesService.ParseWeaponStrength(weapon.Strength, baseS);

        return ApplyMightyBlow(s, isCharging, combatRound, weaponMightyBlow);
    }

    /// <summary>
    /// Calculate effective strength using a resolved <see cref="Weapon"/>.
    /// </summary>
    public int EffectiveStrength(Weapon? weapon, bool isCharging, int combatRound,
        int weaponMightyBlow = 0)
    {
        int baseS = PrimaryProfile.S ?? 3;
        int s = weapon?.Strength.Resolve(baseS) ?? baseS;
        return ApplyMightyBlow(s, isCharging, combatRound, weaponMightyBlow);
    }

    private int ApplyMightyBlow(int strength, bool isCharging, int combatRound, int weaponMightyBlow)
    {
        int unitMB = SpecialRules.MightyBlow ?? 0;

        // Thunderous Charge: Mighty Blow (2) on charge turns only
        if (isCharging && SpecialRules.ThunderousCharge)
            unitMB = Math.Max(unitMB, 2);

        int effectiveMB = Math.Max(unitMB, weaponMightyBlow);
        if (combatRound == 1 && effectiveMB > 0)
            strength += effectiveMB;

        return strength;
    }

    /// <summary>
    /// Calculate effective armour save value (lower is better, 7+ = no save).
    /// Uses a lookup function to get armour save values from core-rules.json.
    /// </summary>
    public int EffectiveArmourSave(int attackerStrength, int attackerAP, int strengthModifier,
        Func<string, string?>? armourSaveLookup = null)
    {
        int totalModifier = ResolvedUnit != null
            ? ComputeResolvedBaseArmourModifier()
            : ComputeBaseArmourModifier(armourSaveLookup);
        return ApplyArmourPenalty(totalModifier, attackerAP, strengthModifier);
    }

    /// <summary>
    /// Fast path: uses a pre-computed base armour modifier to avoid per-call lookups.
    /// </summary>
    public int EffectiveArmourSaveFast(int baseArmourModifier, int attackerAP, int strengthModifier)
    {
        return ApplyArmourPenalty(baseArmourModifier, attackerAP, strengthModifier);
    }

    /// <summary>
    /// Pre-compute the total armour modifier from equipment, natural armour, troop type, and chaos armour.
    /// Call once and cache the result for use with <see cref="EffectiveArmourSaveFast"/>.
    /// </summary>
    public int ComputeBaseArmourModifier(Func<string, string?>? armourSaveLookup = null)
    {
        int totalModifier = 0;
        var effectiveArmour = EquipmentArmour;

        foreach (var armourKey in effectiveArmour)
        {
            string? saveField = armourSaveLookup?.Invoke(armourKey);
            if (saveField != null)
            {
                var (mod, _) = CoreRulesService.ParseArmourSave(saveField);
                totalModifier += mod;
            }
        }

        if (SpecialRules.NaturalArmour.HasValue)
            totalModifier += 7 - SpecialRules.NaturalArmour.Value;

        if (TroopType.IntrinsicArmourSave.HasValue)
            totalModifier += 7 - TroopType.IntrinsicArmourSave.Value;

        if (SpecialRules.ChaosArmour &&
            effectiveArmour.Any(k => k is "lightArmour" or "mediumArmour" or "heavyArmour"))
            totalModifier += 1;

        if (SpecialRules.GromrilArmour &&
            effectiveArmour.Any(k => k is "lightArmour" or "mediumArmour" or "heavyArmour"))
            totalModifier += 1;

        return totalModifier;
    }

    private static int ApplyArmourPenalty(int totalModifier, int attackerAP, int strengthModifier)
    {
        if (totalModifier == 0) return 7;
        int baseSave = 7 - totalModifier;
        int penalty = (-strengthModifier) + attackerAP;
        int modifiedSave = baseSave + penalty;
        return Math.Max(modifiedSave, 2);
    }

    /// <summary>Best WS for combat (max of rider/mount for split profiles).</summary>
    public int BestWS => SplitProfileMax(p => p.WS, 1);

    /// <summary>Best T for combat (max of rider/mount for split profiles), including stance bonuses.</summary>
    public int BestT
    {
        get
        {
            int t = SplitProfileMax(p => p.T, 3);
            if (SelectedTreeAspect == TreeAspect.Oak)
                t += 1;
            return t;
        }
    }

    private int SplitProfileMax(Func<ProfileEntry, int?> stat, int fallback)
    {
        int primary = stat(PrimaryProfile) ?? fallback;
        if (MountProfile != null && TroopType.HasSplitProfile)
            return Math.Max(primary, stat(MountProfile) ?? fallback);
        return primary;
    }

    /// <summary>Number of complete ranks (models / frontage, rounded down).</summary>
    public int Ranks => Frontage > 0 ? ModelCount / Frontage : 1;

    /// <summary>Rank bonus for combat resolution. Max +3, requires minimum rank width.</summary>
    public int RankBonus
    {
        get
        {
            if (Frontage < TroopType.MinRankWidth) return 0;
            return Math.Min(Ranks - 1, 3);
        }
    }

    /// <summary>Number of models that can fight in the front rank.</summary>
    public int ModelsInFrontRank => Math.Min(Frontage, ModelCount);

    /// <summary>
    /// Calculate total attacks from this unit against an opponent.
    /// Weapon rules can provide extra attacks and Fight in Extra Ranks.
    /// </summary>
    public int TotalAttacks(bool isCharging, int combatRound,
        int weaponExtraAttacks = 0, int weaponFiER = 0, int? attacksOverride = null)
    {
        int attacks = attacksOverride ?? PrimaryProfile.A ?? 1;

        // Frenzy: +1 Attack
        if (SpecialRules.Frenzy)
            attacks += 1;

        // Devastating Charge: +1 Attack on charge
        if (isCharging && SpecialRules.DevastatingCharge)
            attacks += 1;

        // Shadow Dance: Storm of Blades (+1 Attack)
        if (SelectedShadowDance == ShadowDance.StormOfBlades)
            attacks += 1;

        // Tree Aspect: Birch (+1 Attack)
        if (SelectedTreeAspect == TreeAspect.Birch)
            attacks += 1;

        // Weapon extra attacks (e.g., Two Hand Weapons: +1 Attack)
        attacks += weaponExtraAttacks;

        // Front rank: full attacks
        int frontRankModels = ModelsInFrontRank;
        int frontRankAttacks = frontRankModels * attacks;

        // Supporting ranks
        int supportingRanks = Math.Max(0, Ranks - 1);

        // Fight in Extra Ranks (unit special rules + weapon, use best)
        int extraRanks = Math.Max(SpecialRules.FightInExtraRanks ?? 0, weaponFiER);

        // Lance Formation: FiER(1) and 3 supporting attacks on charge
        bool lanceCharging = isCharging && SpecialRules.LanceFormation;
        if (lanceCharging)
            extraRanks = Math.Max(extraRanks, 1);

        int maxSupportRanks = 1 + extraRanks;
        int actualSupportRanks = Math.Min(supportingRanks, maxSupportRanks);

        int maxSupportAttacks = lanceCharging ? 3 : TroopType.MaxSupportingAttacks;
        int supportAttacksPerModel = Math.Min(attacks, maxSupportAttacks);
        int supportingModels = actualSupportRanks * Frontage;
        int supportAttacks = supportingModels * supportAttacksPerModel;

        return frontRankAttacks + supportAttacks;
    }

    /// <summary>
    /// Calculate mount attacks including supporting ranks.
    /// </summary>
    public int MountAttacks()
    {
        if (MountProfile == null || !TroopType.HasSplitProfile) return 0;
        int mountA = MountProfile.A ?? 1;

        int frontAttacks = ModelsInFrontRank * mountA;

        int supportingRanks = Math.Max(0, Ranks - 1);
        int actualSupportRanks = Math.Min(supportingRanks, 1);

        int mountSupportPerModel = TroopType.BaseType switch
        {
            BaseTroopType.Cavalry => 1,
            BaseTroopType.MonstrousCavalry => 2,
            _ => 0
        };
        int supportModels = actualSupportRanks * Frontage;
        int supportAttacks = supportModels * mountSupportPerModel;

        return frontAttacks + supportAttacks;
    }

    public static (ProfileEntry Primary, ProfileEntry? Mount) ResolveProfiles(UnitEntry unit)
    {
        var profiles = unit.Profiles;
        if (profiles.Count == 0)
            throw new InvalidOperationException($"Unit {unit.Name} has no profiles");
        if (profiles.Count == 1)
            return (profiles[0], null);

        var primary = profiles.FirstOrDefault(p => p.Points.HasValue) ?? profiles[0];
        var mount = profiles.FirstOrDefault(p => p != primary);

        return (primary, mount);
    }

    // ─── Points Cost ─────────────────────────────────────────────

    public double BaseModelCost => ResolvedUnit?.BaseModelCost ?? UnitEntry.PointsCost ?? PrimaryProfile.Points ?? 0;

    /// <summary>Total points cost of all selected equipment upgrades.</summary>
    public double EquipmentUpgradeCost
    {
        get
        {
            if (UnitEntry.Options == null) return 0;
            double total = 0;

            for (int i = 0; i < UnitEntry.Options.Count; i++)
            {
                var option = UnitEntry.Options[i];
                if (option.Type != "equipment" || option.Choices == null) continue;
                if (!SelectedUpgrades.TryGetValue(i, out int choiceIdx) || choiceIdx < 0) continue;
                if (choiceIdx >= option.Choices.Count) continue;

                var choice = option.Choices[choiceIdx];
                var pts = choice.Points ?? 0;
                total += choice.PerModel ? pts * ModelCount : pts;
            }

            return total;
        }
    }

    // ─── Command group ───────────────────────────────────────────

    public bool CanHaveChampion => ResolvedUnit?.Command.CanHaveChampion
        ?? UnitEntry.Options?.Any(o =>
            o.Type == "command" && o.Role != null
            && (o.Role.Contains("Leader", StringComparison.OrdinalIgnoreCase)
                || o.Role.Contains("Champion", StringComparison.OrdinalIgnoreCase))) ?? false;

    public bool CanHaveMusician => ResolvedUnit?.Command.CanHaveMusician
        ?? UnitEntry.Options?.Any(o =>
            o.Type == "command" && o.Role != null
            && o.Role.Contains("Musician", StringComparison.OrdinalIgnoreCase)) ?? false;

    public bool CanHaveStandardBearer => ResolvedUnit?.Command.CanHaveStandardBearer
        ?? UnitEntry.Options?.Any(o =>
            o.Type == "command" && o.Role != null
            && o.Role.Contains("Standard Bearer", StringComparison.OrdinalIgnoreCase)) ?? false;

    public (double Champion, double Musician, double Standard) CommandGroupCosts
    {
        get
        {
            if (ResolvedUnit != null)
            {
                var cmd = ResolvedUnit.Command;
                return (cmd.ChampionCost, cmd.MusicianCost, cmd.StandardBearerCost);
            }

            double champ = 0, mus = 0, std = 0;
            if (UnitEntry.Options == null) return (0, 0, 0);

            foreach (var opt in UnitEntry.Options)
            {
                if (opt.Type != "command" || opt.Role == null) continue;
                double cost = opt.Points ?? 0;
                var role = opt.Role;

                if (role.Contains("Leader", StringComparison.OrdinalIgnoreCase)
                    || role.Contains("Champion", StringComparison.OrdinalIgnoreCase))
                    champ = cost;
                else if (role.Contains("Musician", StringComparison.OrdinalIgnoreCase))
                    mus = cost;
                else if (role.Contains("Standard Bearer", StringComparison.OrdinalIgnoreCase))
                    std = cost;
            }

            return (champ, mus, std);
        }
    }

    public double TotalPointsCost
    {
        get
        {
            double baseCost = BaseModelCost * ModelCount;
            double equipmentCost = EquipmentUpgradeCost;
            var (champCost, musCost, stdCost) = CommandGroupCosts;
            double commandCost = (HasChampion ? champCost : 0)
                               + (HasMusician ? musCost : 0)
                               + (HasStandardBearer ? stdCost : 0);
            return baseCost + equipmentCost + commandCost;
        }
    }

    // ─── Static helpers ──────────────────────────────────────────

    public static int ParseBaseWidthMm(string? baseSize)
    {
        if (string.IsNullOrWhiteSpace(baseSize)) return 25;
        var firstLine = baseSize.Split('\n')[0];
        var match = Regex.Match(firstLine, @"(\d+)x(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 25;
    }

    public static int ParseMinUnitSize(string? unitSize)
    {
        if (string.IsNullOrWhiteSpace(unitSize)) return 1;
        var match = Regex.Match(unitSize, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }
}
