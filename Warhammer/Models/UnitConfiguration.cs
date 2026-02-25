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

    // Computed
    public int Ranks => ModelCount <= 0 ? 0 : (int)Math.Ceiling((double)ModelCount / Files);
    public int ModelsInFrontRank => Math.Min(ModelCount, Files);
    public int CompleteRanks => Files <= 0 ? 0 : ModelCount / Files;
    public int RankBonus => Math.Min(Math.Max(CompleteRanks - 1, 0), 3);

    public int EffectiveStrength =>
        (PrimaryProfile.S ?? 3) + (SelectedWeapon?.StrengthBonus ?? 0);

    public int EffectiveInitiative
    {
        get
        {
            if (SelectedWeapon?.AlwaysStrikesLast == true) return 1;
            return (PrimaryProfile.I ?? 1) + (SelectedWeapon?.InitiativeModifier ?? 0);
        }
    }

    public CombatAttackBreakdown CalculateAttacks()
    {
        var breakdown = new CombatAttackBreakdown();
        int baseAttacks = PrimaryProfile.A ?? 1;

        // Front rank: all models attack with full A characteristic
        int frontRankModels = ModelsInFrontRank;
        breakdown.FrontRankAttacks = frontRankModels * baseAttacks;

        // Champion: +1A on one model in front rank
        if (HasChampion && frontRankModels > 0)
            breakdown.ChampionBonusAttacks = 1;

        // Supporting ranks: 1 attack per model (not full A)
        int extraFightingRanks = SelectedWeapon?.FightInExtraRanks ?? 0;
        int totalFightingRanks = 1 + extraFightingRanks;
        int supportingFightingRanks = Math.Min(totalFightingRanks - 1, Math.Max(Ranks - 1, 0));

        for (int rank = 1; rank <= supportingFightingRanks; rank++)
        {
            int startOfRank = Files * rank;
            int modelsInRank = Math.Min(Files, Math.Max(ModelCount - startOfRank, 0));
            if (modelsInRank <= 0) break;
            breakdown.SupportingRankAttacks += modelsInRank;
        }

        // Mount attacks: front rank mounts
        if (MountProfile?.A > 0)
            breakdown.MountAttacks = frontRankModels * (MountProfile.A ?? 0);

        return breakdown;
    }

    public int CalculateArmourSave(int attackerStrength, int armourPiercing = 0)
    {
        int totalArmourBonus = ArmourPieces.Sum(a => a.SaveBonus);
        int baseSave = totalArmourBonus > 0 ? 7 - totalArmourBonus : 7;

        if (TroopType?.IntrinsicArmourSave.HasValue == true)
            baseSave = Math.Min(baseSave, TroopType.IntrinsicArmourSave.Value);

        int strengthMod = CoreRulesService.GetArmourSaveModifier(attackerStrength);
        int effectiveSave = baseSave - strengthMod + armourPiercing;

        return Math.Max(effectiveSave, 2); // 1 always fails
    }
}

public class CombatAttackBreakdown
{
    public int FrontRankAttacks { get; set; }
    public int ChampionBonusAttacks { get; set; }
    public int SupportingRankAttacks { get; set; }
    public int MountAttacks { get; set; }

    public int TotalModelAttacks =>
        FrontRankAttacks + ChampionBonusAttacks + SupportingRankAttacks;

    public int TotalAttacks =>
        FrontRankAttacks + ChampionBonusAttacks + SupportingRankAttacks + MountAttacks;
}
