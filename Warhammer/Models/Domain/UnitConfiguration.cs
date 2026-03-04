using System.Text.RegularExpressions;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Models.Domain;

public class UnitConfiguration
{
    public UnitEntry Unit { get; }
    public ProfileEntry PrimaryProfile { get; }
    public ProfileEntry? MountProfile { get; }

    // User configuration
    public int ModelCount { get; set; }
    public int Frontage { get; set; }
    public string SelectedWeaponKey { get; set; }
    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }

    // Parsed data
    public TroopTypeInfo TroopType { get; }
    public int BaseWidthMm { get; }
    public ParsedSpecialRules SpecialRules { get; }
    public List<string> EquipmentWeapons { get; }
    public List<string> EquipmentArmour { get; }

    public UnitConfiguration(UnitEntry unit)
    {
        Unit = unit;
        (PrimaryProfile, MountProfile) = ResolveProfiles(unit);
        TroopType = TroopTypeInfo.Parse(unit.TroopType);
        BaseWidthMm = ParseBaseWidthMm(unit.BaseSize);
        SpecialRules = ParsedSpecialRules.Parse(unit.SpecialRules);
        (EquipmentWeapons, EquipmentArmour) = EquipmentParser.Parse(unit.Equipment);

        // Defaults
        ModelCount = TroopType.IsCharacter ? 1 : ParseMinUnitSize(unit.UnitSize);
        Frontage = TroopType.IsCharacter ? 1 : TroopType.MinRankWidth;
        SelectedWeaponKey = EquipmentWeapons.FirstOrDefault() ?? "handWeapon";
    }

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

        // Mighty Blow: +X Strength in first round of combat
        // Use highest of unit MB and weapon MB (don't stack)
        int unitMB = SpecialRules.MightyBlow ?? 0;
        int effectiveMB = Math.Max(unitMB, weaponMightyBlow);
        if (combatRound == 1 && effectiveMB > 0)
            s += effectiveMB;

        return s;
    }

    /// <summary>
    /// Calculate effective armour save value (lower is better, 7+ = no save).
    /// Takes into account all equipment armour, natural armour, and strength/AP modifiers.
    /// Uses a lookup function to get armour save values from core-rules.json.
    /// </summary>
    public int EffectiveArmourSave(int attackerStrength, int attackerAP, int strengthModifier,
        Func<string, string?>? armourSaveLookup = null)
    {
        int totalModifier = 0;

        // Equipment armour
        foreach (var armourKey in EquipmentArmour)
        {
            string? saveField = armourSaveLookup?.Invoke(armourKey);
            if (saveField != null)
            {
                var (mod, _) = CoreRulesService.ParseArmourSave(saveField);
                totalModifier += mod;
            }
        }

        // Natural armour
        if (SpecialRules.NaturalArmour.HasValue)
        {
            // Natural Armour (5+) gives +2 modifier (7 - 5 = 2)
            totalModifier += 7 - SpecialRules.NaturalArmour.Value;
        }

        // Intrinsic armour from troop type (e.g., Chariot Armour Save 4+)
        if (TroopType.IntrinsicArmourSave.HasValue)
        {
            totalModifier += 7 - TroopType.IntrinsicArmourSave.Value;
        }

        if (totalModifier == 0) return 7; // no save

        int baseSave = 7 - totalModifier;

        // Apply strength modifier and AP
        int modifiedSave = baseSave - strengthModifier - attackerAP;

        return Math.Max(modifiedSave, 2); // 1+ is impossible, minimum useful save is 2+
    }

    /// <summary>
    /// Number of complete ranks (models / frontage, rounded down).
    /// </summary>
    public int Ranks => Frontage > 0 ? ModelCount / Frontage : 1;

    /// <summary>
    /// Rank bonus for combat resolution. Max +3, requires minimum rank width.
    /// </summary>
    public int RankBonus
    {
        get
        {
            if (Frontage < TroopType.MinRankWidth) return 0;
            return Math.Min(Ranks - 1, 3);
        }
    }

    /// <summary>
    /// Number of models that can fight in the front rank.
    /// </summary>
    public int ModelsInFrontRank => Math.Min(Frontage, ModelCount);

    /// <summary>
    /// Calculate total attacks from this unit against an opponent.
    /// Weapon rules can provide extra attacks and Fight in Extra Ranks.
    /// </summary>
    public int TotalAttacks(bool isCharging, int combatRound,
        int weaponExtraAttacks = 0, int weaponFiER = 0)
    {
        int attacks = PrimaryProfile.A ?? 1;

        // Frenzy: +1 Attack
        if (SpecialRules.Frenzy)
            attacks += 1;

        // Devastating Charge: +1 Attack on charge
        if (isCharging && SpecialRules.DevastatingCharge)
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
        int maxSupportRanks = 1 + extraRanks; // base 1 supporting rank + extra
        int actualSupportRanks = Math.Min(supportingRanks, maxSupportRanks);

        int supportAttacksPerModel = Math.Min(attacks, TroopType.MaxSupportingAttacks);
        int supportingModels = actualSupportRanks * Frontage;
        int supportAttacks = supportingModels * supportAttacksPerModel;

        return frontRankAttacks + supportAttacks;
    }

    /// <summary>
    /// Calculate mount attacks including supporting ranks.
    /// Cavalry: 1 mount support attack per supporting model.
    /// Monstrous Cavalry: 2 mount support attacks per supporting model.
    /// </summary>
    public int MountAttacks()
    {
        if (MountProfile == null || !TroopType.HasSplitProfile) return 0;
        int mountA = MountProfile.A ?? 1;

        // Front rank: full mount attacks
        int frontAttacks = ModelsInFrontRank * mountA;

        // Supporting ranks: mount support attacks per model
        int supportingRanks = Math.Max(0, Ranks - 1);
        int actualSupportRanks = Math.Min(supportingRanks, 1); // base 1 support rank

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

    // --- Points Cost ---

    public double BaseModelCost => Unit.PointsCost ?? PrimaryProfile.Points ?? 0;

    public double WeaponUpgradeCost
    {
        get
        {
            if (Unit.Options == null) return 0;
            var searchTerms = SelectedWeaponKey switch
            {
                "spear" => new[] { "spear" },
                "greatWeapon" => new[] { "great weapon" },
                "polearm" => new[] { "polearm" },
                "two_additionalHandWeapons" => new[] { "additional hand weapon", "two hand weapons" },
                "flail" => new[] { "flail" },
                _ => Array.Empty<string>()
            };
            if (searchTerms.Length == 0) return 0;
            foreach (var opt in Unit.Options)
            {
                var lower = opt.ToLowerInvariant();
                if (searchTerms.Any(t => lower.Contains(t)))
                {
                    var (cost, isPerModel) = ParseOptionCost(opt);
                    return isPerModel ? cost * ModelCount : cost;
                }
            }
            return 0;
        }
    }

    // Command group availability — derived from unit options
    public bool CanHaveChampion => Unit.Options?.Any(opt =>
    {
        var lower = opt.ToLowerInvariant();
        return (lower.Contains("leader") || lower.Contains("champion")) && !lower.Contains("standard");
    }) ?? false;

    public bool CanHaveMusician => Unit.Options?.Any(opt =>
        opt.ToLowerInvariant().Contains("musician")) ?? false;

    public bool CanHaveStandardBearer => Unit.Options?.Any(opt =>
        opt.ToLowerInvariant().Contains("standard bearer")) ?? false;

    public (double Champion, double Musician, double Standard) CommandGroupCosts
    {
        get
        {
            double champ = 0, mus = 0, std = 0;
            if (Unit.Options == null) return (0, 0, 0);
            foreach (var opt in Unit.Options)
            {
                var lower = opt.ToLowerInvariant();
                if ((lower.Contains("leader") || lower.Contains("champion")) && !lower.Contains("standard"))
                {
                    var (cost, _) = ParseOptionCost(opt);
                    champ = cost;
                }
                else if (lower.Contains("musician"))
                {
                    var (cost, _) = ParseOptionCost(opt);
                    mus = cost;
                }
                else if (lower.Contains("standard bearer"))
                {
                    var (cost, _) = ParseOptionCost(opt);
                    std = cost;
                }
            }
            return (champ, mus, std);
        }
    }

    public double TotalPointsCost
    {
        get
        {
            double baseCost = BaseModelCost * ModelCount;
            double weaponCost = WeaponUpgradeCost;
            var (champCost, musCost, stdCost) = CommandGroupCosts;
            double commandCost = (HasChampion ? champCost : 0)
                               + (HasMusician ? musCost : 0)
                               + (HasStandardBearer ? stdCost : 0);
            return baseCost + weaponCost + commandCost;
        }
    }

    /// <summary>
    /// Parse points cost from option text. Returns (cost, isPerModel).
    /// Patterns: "+3 points/model", "+0.5 point/model", "+12 points"
    /// </summary>
    public static (double Cost, bool IsPerModel) ParseOptionCost(string optionText)
    {
        var perModel = Regex.Match(optionText, @"\+(\d+\.?\d*)\s+points?/model");
        if (perModel.Success)
            return (double.Parse(perModel.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), true);

        var flat = Regex.Match(optionText, @"\+(\d+\.?\d*)\s+points?");
        if (flat.Success)
            return (double.Parse(flat.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), false);

        return (0, false);
    }

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
