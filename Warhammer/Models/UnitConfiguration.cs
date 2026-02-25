namespace Warhammer.Models;

using Warhammer.Services;

public class UnitConfiguration
{
    public ExtractedUnit SourceUnit { get; set; } = null!;
    public string FactionName { get; set; } = "";

    // User selections
    public int ModelCount { get; set; } = 10;
    public int Files { get; set; } = 5;
    public WeaponDefinition SelectedWeapon { get; set; } = null!;
    public List<ArmourDefinition> ArmourPieces { get; set; } = new();
    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }

    public ParsedTroopType TroopType { get; set; } = null!;
    public ExtractedProfile PrimaryProfile => SourceUnit.PrimaryProfile;
    public ExtractedProfile? MountProfile => SourceUnit.MountProfile;
    public int BaseWidthMm => SourceUnit.BaseWidthMm;

    // Parsed special rules (lazy-initialized)
    private ParsedSpecialRules? _parsedRules;
    public ParsedSpecialRules SpecialRules =>
        _parsedRules ??= SpecialRulesParser.Parse(SourceUnit.SpecialRules);

    // Computed
    public int Ranks => ModelCount <= 0 ? 0 : (int)Math.Ceiling((double)ModelCount / Files);
    public int ModelsInFrontRank => Math.Min(ModelCount, Files);
    public int CompleteRanks => Files <= 0 ? 0 : ModelCount / Files;
    public int RankBonus
    {
        get
        {
            int minWidth = TroopTypeParser.GetMinimumRankWidth(TroopType?.BaseType ?? BaseTroopType.Unknown);
            if (minWidth == 0 || Files < minWidth) return 0;
            return Math.Min(Math.Max(CompleteRanks - 1, 0), 3);
        }
    }

    /// <summary>
    /// Simple property for display purposes (no combat context, shows base + weapon bonus only).
    /// </summary>
    public int EffectiveStrength =>
        (PrimaryProfile.S ?? 3) + (SelectedWeapon?.StrengthBonus ?? 0);

    /// <summary>
    /// Context-aware strength for combat calculations.
    /// </summary>
    public int GetEffectiveStrength(bool isFirstRound, bool charged, bool wasCharged, string? defenderBaseType)
    {
        int baseStrength = PrimaryProfile.S ?? 3;
        int weaponBonus = SelectedWeapon?.GetStrengthBonus(isFirstRound, charged, wasCharged, defenderBaseType) ?? 0;

        // Chaos Armour special rule: +1 to armour save, NOT strength. Handled in armour save calc.
        return baseStrength + weaponBonus;
    }

    /// <summary>
    /// Simple property for display purposes.
    /// </summary>
    public int EffectiveInitiative
    {
        get
        {
            if (SelectedWeapon?.AlwaysStrikesLast == true) return 1;
            return (PrimaryProfile.I ?? 1) + (SelectedWeapon?.InitiativeModifier ?? 0);
        }
    }

    /// <summary>
    /// Context-aware initiative for combat calculations.
    /// </summary>
    public int GetEffectiveInitiative(bool charged, bool wasCharged)
    {
        if (SelectedWeapon?.AlwaysStrikesLast == true) return 1;
        int baseInit = PrimaryProfile.I ?? 1;
        int weaponMod = SelectedWeapon?.GetInitiativeModifier(charged, wasCharged) ?? 0;
        return baseInit + weaponMod;
    }

    /// <summary>
    /// Calculate attacks. When opponent is provided, limits front rank by base contact.
    /// Without opponent, returns unit's own maximum attacks (for display purposes).
    /// </summary>
    public CombatAttackBreakdown CalculateAttacks(UnitConfiguration? opponent = null)
    {
        var breakdown = new CombatAttackBreakdown();
        int baseAttacks = PrimaryProfile.A ?? 1;
        if (SpecialRules.Frenzy) baseAttacks += 1;

        // Determine how many front rank models can fight
        int frontRankModels = ModelsInFrontRank;
        if (opponent != null)
        {
            // Base contact limiting: only models physically touching the enemy can fight
            int defenderWidthMm = opponent.ModelsInFrontRank * opponent.BaseWidthMm;
            int maxInContact = (int)Math.Ceiling((double)defenderWidthMm / BaseWidthMm) + 2;
            frontRankModels = Math.Min(frontRankModels, maxInContact);
        }

        breakdown.FrontRankAttacks = frontRankModels * baseAttacks;

        // Supporting ranks: 1 attack per model (not full A), limited to fighting frontage
        int extraFightingRanks = SelectedWeapon?.FightInExtraRanks ?? 0;
        int totalFightingRanks = 1 + extraFightingRanks;
        int supportingFightingRanks = Math.Min(totalFightingRanks - 1, Math.Max(Ranks - 1, 0));

        for (int rank = 1; rank <= supportingFightingRanks; rank++)
        {
            int startOfRank = Files * rank;
            int modelsInRank = Math.Min(frontRankModels, Math.Max(ModelCount - startOfRank, 0));
            if (modelsInRank <= 0) break;
            breakdown.SupportingRankAttacks += modelsInRank;
        }

        // Mount attacks: front rank mounts in base contact
        if (MountProfile?.A > 0)
            breakdown.MountAttacks = frontRankModels * (MountProfile.A ?? 0);

        return breakdown;
    }

    public int CalculateArmourSave(int attackerStrength, int armourPiercing, CoreRulesService rules)
    {
        int totalArmourBonus = ArmourPieces.Sum(a => a.SaveBonus);

        // Natural Armour: stacks with equipment (e.g., 5+ = +2 bonus)
        if (SpecialRules.NaturalArmour.HasValue)
            totalArmourBonus += 7 - SpecialRules.NaturalArmour.Value;

        // Chaos Armour special rule: +1 save if wearing light/medium/heavy armour
        if (SpecialRules.ChaosArmour && ArmourPieces.Any(a =>
            a.Key is "lightArmour" or "mediumArmour" or "heavyArmour"))
        {
            totalArmourBonus += 1;
        }

        int baseSave = totalArmourBonus > 0 ? 7 - totalArmourBonus : 7;

        if (TroopType?.IntrinsicArmourSave.HasValue == true)
            baseSave = Math.Min(baseSave, TroopType.IntrinsicArmourSave.Value);

        int strengthPenalty = rules.GetArmourSavePenalty(attackerStrength);
        int effectiveSave = baseSave + strengthPenalty + armourPiercing;

        return Math.Max(effectiveSave, 2); // natural 1 always fails
    }
}

public class CombatAttackBreakdown
{
    public int FrontRankAttacks { get; set; }
    public int SupportingRankAttacks { get; set; }
    public int MountAttacks { get; set; }

    public int TotalModelAttacks =>
        FrontRankAttacks + SupportingRankAttacks;

    public int TotalAttacks =>
        FrontRankAttacks + SupportingRankAttacks + MountAttacks;
}
