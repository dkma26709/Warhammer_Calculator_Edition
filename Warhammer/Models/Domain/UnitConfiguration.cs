using System.Text.RegularExpressions;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Models.Domain;

public class UnitConfiguration
{
    // ─── Source data ─────────────────────────────────────────────

    /// <summary>The fully-resolved unit template.</summary>
    public Unit ResolvedUnit { get; }

    /// <summary>The raw JSON unit entry (from <see cref="Unit.RawEntry"/>).</summary>
    public UnitEntry UnitEntry { get; }

    public ProfileEntry PrimaryProfile { get; }
    public ProfileEntry? MountProfile { get; }

    // ─── User configuration ─────────────────────────────────────

    public int ModelCount { get; set; }
    public int Frontage { get; set; }

    /// <summary>Selected weapon as enum key.</summary>
    public CloseCombatWeaponKey SelectedWeapon { get; set; }

    /// <summary>String accessor for <see cref="SelectedWeapon"/> — used by Blazor select binding.</summary>
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

    /// <summary>Selected magic items with their configured combat modifiers.</summary>
    public List<SelectedMagicItem> SelectedMagicItems { get; } = [];

    /// <summary>Generic modifiers applied to this unit (from magic items, spells, or manual entry).</summary>
    public List<UnitModifier> Modifiers { get; } = [];

    // ─── Parsed base data ────────────────────────────────────────

    public TroopTypeInfo TroopType { get; }
    public int BaseWidthMm { get; }
    public ParsedSpecialRules SpecialRules { get; }
    public List<CloseCombatWeaponKey> BaseWeaponKeys { get; }
    public List<ArmourKey> BaseArmourKeys { get; }

    // ─── Modifier system ─────────────────────────────────────────

    /// <summary>All modifiers combined: magic item modifiers + standalone modifiers.</summary>
    private IEnumerable<UnitModifier> AllModifiers =>
        SelectedMagicItems.Select(mi => mi.Modifier).Concat(Modifiers);

    /// <summary>Aggregate of all modifiers — stat bonuses summed, booleans ORed, saves take best.</summary>
    public UnitModifier AggregateModifier => UnitModifier.Aggregate(AllModifiers);

    /// <summary>
    /// Base special rules merged with all modifier rule overrides.
    /// Use this instead of <see cref="SpecialRules"/> in combat calculations.
    /// </summary>
    public ParsedSpecialRules EffectiveSpecialRules
    {
        get
        {
            var aggregate = AggregateModifier;
            return aggregate.RuleOverrides != null
                ? SpecialRules.Merge(aggregate.RuleOverrides)
                : SpecialRules;
        }
    }

    // ─── Constructors ────────────────────────────────────────────

    /// <summary>
    /// Construct from a fully-resolved <see cref="Domain.Unit"/>.
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
        BaseWeaponKeys = unit.BaseModel.Weapons.Select(w => w.Key).ToList();
        BaseArmourKeys = unit.BaseModel.Armour.Select(a => a.Key).ToList();

        // Defaults
        ModelCount = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.MinUnitSize;
        Frontage = unit.BaseModel.TroopType.IsCharacter ? 1 : unit.BaseModel.TroopType.MinRankWidth;
        SelectedWeapon = unit.BaseModel.Weapons
            .Select(w => w.Key)
            .FirstOrDefault(CloseCombatWeaponKey.HandWeapon);
    }

    // ─── Resolved equipment access ───────────────────────────────

    /// <summary>Get the selected weapon as a resolved <see cref="Weapon"/> domain object.</summary>
    public Weapon? GetSelectedResolvedWeapon()
    {
        return ResolvedUnit.BaseModel.Weapons
            .FirstOrDefault(w => w.Key == SelectedWeapon);
    }

    /// <summary>
    /// Pre-compute base armour modifier from resolved <see cref="Armour"/> objects,
    /// including equipment upgrades. Uses <see cref="Unit.AvailableArmour"/> for lookup.
    /// </summary>
    public int ComputeBaseArmourModifier()
    {
        int totalModifier = 0;
        var effectiveArmour = EffectiveArmour;

        foreach (var armourKey in effectiveArmour)
        {
            if (ResolvedUnit.AvailableArmour.TryGetValue(armourKey, out var armour))
                totalModifier += armour.CombatSaveModifier;
        }

        var rules = EffectiveSpecialRules;

        if (rules.NaturalArmour.HasValue)
            totalModifier += 7 - rules.NaturalArmour.Value;

        if (TroopType.IntrinsicArmourSave.HasValue)
            totalModifier += 7 - TroopType.IntrinsicArmourSave.Value;

        bool hasConventionalArmour = effectiveArmour
            .Any(k => k is ArmourKey.LightArmour or ArmourKey.MediumArmour or ArmourKey.HeavyArmour);

        if (rules.ChaosArmour && hasConventionalArmour)
            totalModifier += 1;

        if (rules.GromrilArmour && hasConventionalArmour)
            totalModifier += 1;

        totalModifier += AggregateModifier.ArmourSaveBonus ?? 0;

        return totalModifier;
    }

    // ─── Effective Equipment (base + upgrades - replacements) ────

    /// <summary>
    /// All close combat weapon keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<CloseCombatWeaponKey> EffectiveWeapons
    {
        get
        {
            var weapons = new List<CloseCombatWeaponKey>(BaseWeaponKeys);
            ApplyWeaponUpgrades(weapons);
            return weapons;
        }
    }

    /// <summary>
    /// All armour keys the unit currently has, considering base equipment and selected upgrades.
    /// </summary>
    public List<ArmourKey> EffectiveArmour
    {
        get
        {
            var armour = new List<ArmourKey>(BaseArmourKeys);
            ApplyArmourUpgrades(armour);
            return armour;
        }
    }

    private void ApplyWeaponUpgrades(List<CloseCombatWeaponKey> keys)
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
                    && replacedIsWeapon
                    && CloseCombatWeaponKeyExtensions.TryFromJsonKey(replacedKey, out var replacedEnumKey))
                {
                    keys.Remove(replacedEnumKey);
                }
            }

            // Add the selected items
            foreach (var name in choice.Name)
            {
                if (EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon)
                    && itemIsWeapon
                    && CloseCombatWeaponKeyExtensions.TryFromJsonKey(key, out var enumKey)
                    && !keys.Contains(enumKey))
                {
                    keys.Add(enumKey);
                }
            }
        }
    }

    private void ApplyArmourUpgrades(List<ArmourKey> keys)
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
                    && !replacedIsWeapon
                    && ArmourKeyExtensions.TryFromJsonKey(replacedKey, out var replacedEnumKey))
                {
                    keys.Remove(replacedEnumKey);
                }
            }

            // Add the selected items
            foreach (var name in choice.Name)
            {
                if (EquipmentParser.TryNormalize(name, out var key, out bool itemIsWeapon)
                    && !itemIsWeapon
                    && ArmourKeyExtensions.TryFromJsonKey(key, out var enumKey)
                    && !keys.Contains(enumKey))
                {
                    keys.Add(enumKey);
                }
            }
        }
    }

    // ─── Combat Stats ────────────────────────────────────────────

    /// <summary>
    /// Calculate effective strength for a given weapon.
    /// Weapon Mighty Blow and unit Mighty Blow use the highest value (don't stack).
    /// </summary>
    public int EffectiveStrength(Weapon? weapon, bool isCharging, int combatRound,
        int weaponMightyBlow = 0)
    {
        int baseS = (PrimaryProfile.S ?? 3) + (AggregateModifier.StrengthBonus ?? 0);
        int s = weapon?.Strength.Resolve(baseS) ?? baseS;
        return ApplyMightyBlow(s, isCharging, combatRound, weaponMightyBlow);
    }

    private int ApplyMightyBlow(int strength, bool isCharging, int combatRound, int weaponMightyBlow)
    {
        int unitMB = EffectiveSpecialRules.MightyBlow ?? 0;

        // Thunderous Charge: Mighty Blow (2) on charge turns only
        if (isCharging && EffectiveSpecialRules.ThunderousCharge)
            unitMB = Math.Max(unitMB, 2);

        int effectiveMB = Math.Max(unitMB, weaponMightyBlow);
        if (combatRound == 1 && effectiveMB > 0)
            strength += effectiveMB;

        return strength;
    }

    /// <summary>Calculate effective armour save value (lower is better, 7+ = no save).</summary>
    public int EffectiveArmourSave(int attackerStrength, int attackerAP, int strengthModifier)
    {
        int totalModifier = ComputeBaseArmourModifier();
        return ApplyArmourPenalty(totalModifier, attackerAP, strengthModifier);
    }

    /// <summary>
    /// Fast path: uses a pre-computed base armour modifier to avoid per-call lookups.
    /// </summary>
    public int EffectiveArmourSaveFast(int baseArmourModifier, int attackerAP, int strengthModifier)
    {
        return ApplyArmourPenalty(baseArmourModifier, attackerAP, strengthModifier);
    }

    private static int ApplyArmourPenalty(int totalModifier, int attackerAP, int strengthModifier)
    {
        if (totalModifier == 0) return 7;
        int baseSave = 7 - totalModifier;
        int penalty = (-strengthModifier) + attackerAP;
        int modifiedSave = baseSave + penalty;
        return Math.Max(modifiedSave, 2);
    }

    /// <summary>Best WS for combat (max of rider/mount for split profiles), including modifier bonuses.</summary>
    public int BestWS => SplitProfileMax(p => p.WS, 1) + (AggregateModifier.WeaponSkillBonus ?? 0);

    /// <summary>Best T for combat (max of rider/mount for split profiles), including stance and modifier bonuses.</summary>
    public int BestT
    {
        get
        {
            int t = SplitProfileMax(p => p.T, 3);
            if (SelectedTreeAspect == TreeAspect.Oak)
                t += 1;
            t += AggregateModifier.ToughnessBonus ?? 0;
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
        int attacks = (attacksOverride ?? PrimaryProfile.A ?? 1) + (AggregateModifier.AttacksBonus ?? 0);

        var rules = EffectiveSpecialRules;

        // Frenzy: +1 Attack
        if (rules.Frenzy)
            attacks += 1;

        // Devastating Charge: +1 Attack on charge
        if (isCharging && rules.DevastatingCharge)
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
        int extraRanks = Math.Max(rules.FightInExtraRanks ?? 0, weaponFiER);

        // Lance Formation: FiER(1) and 3 supporting attacks on charge
        bool lanceCharging = isCharging && rules.LanceFormation;
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

    public double BaseModelCost => ResolvedUnit.BaseModelCost;

    /// <summary>Total points cost of all selected upgrades (equipment, mounts, upgrades, etc.).</summary>
    public double UpgradeCost
    {
        get
        {
            if (UnitEntry.Options == null) return 0;
            double total = 0;

            for (int i = 0; i < UnitEntry.Options.Count; i++)
            {
                var option = UnitEntry.Options[i];
                if (!SelectedUpgrades.TryGetValue(i, out int choiceIdx) || choiceIdx < 0) continue;

                switch (option.Type)
                {
                    case "equipment" or "mount":
                        if (option.Choices == null || choiceIdx >= option.Choices.Count) continue;
                        var choice = option.Choices[choiceIdx];
                        var choicePts = choice.Points ?? 0;
                        total += choice.PerModel ? choicePts * ModelCount : choicePts;
                        break;

                    case "upgrade" when option.SubOptions != null:
                        if (choiceIdx >= option.SubOptions.Count) continue;
                        var subOption = option.SubOptions[choiceIdx];
                        var subChoice = subOption.Choices?.FirstOrDefault();
                        var subPts = subChoice?.Points ?? subOption.Points ?? 0;
                        var subPerModel = subChoice?.PerModel ?? subOption.PerModel ?? false;
                        total += subPerModel ? subPts * ModelCount : subPts;
                        break;

                    case "upgrade" or "wizard_upgrade" or "blessed_spawning":
                        var flatPts = option.Points ?? 0;
                        var flatPerModel = option.PerModel ?? false;
                        total += flatPerModel ? flatPts * ModelCount : flatPts;
                        break;
                }
            }

            return total;
        }
    }

    // ─── Command group ───────────────────────────────────────────

    public bool CanHaveChampion => ResolvedUnit.Command.CanHaveChampion;
    public bool CanHaveMusician => ResolvedUnit.Command.CanHaveMusician;
    public bool CanHaveStandardBearer => ResolvedUnit.Command.CanHaveStandardBearer;

    public (double Champion, double Musician, double Standard) CommandGroupCosts
    {
        get
        {
            var cmd = ResolvedUnit.Command;
            return (cmd.ChampionCost, cmd.MusicianCost, cmd.StandardBearerCost);
        }
    }

    public double TotalPointsCost
    {
        get
        {
            double baseCost = BaseModelCost * ModelCount;
            double equipmentCost = UpgradeCost;
            var (champCost, musCost, stdCost) = CommandGroupCosts;
            double commandCost = (HasChampion ? champCost : 0)
                               + (HasMusician ? musCost : 0)
                               + (HasStandardBearer ? stdCost : 0);
            double magicItemsCost = SelectedMagicItems.Sum(mi => mi.PointsCost);
            return baseCost + equipmentCost + commandCost + magicItemsCost;
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
