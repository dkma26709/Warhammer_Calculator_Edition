namespace Warhammer.Models;

public class WeaponDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCloseCombat { get; set; }

    // Base strength bonus (always applies, e.g. halberd +1, great weapon +2)
    public int StrengthBonus { get; set; }

    // Round-dependent strength (flail: +2 first / +1 subsequent, morning star: +1 first / 0 subsequent)
    public bool HasRoundDependentStrength { get; set; }
    public int StrengthBonusFirstRound { get; set; }
    public int StrengthBonusSubsequent { get; set; }

    // Mighty Blow: conditional +X Strength
    public int MightyBlow { get; set; }
    public bool MightyBlowOnCharge { get; set; }        // lances
    public bool MightyBlowWhenCharged { get; set; }     // spear/pike
    // Mighty Blow from spear/pike does not apply vs these troop types
    public static readonly HashSet<string> MightyBlowExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
        { nameof(BaseTroopType.Infantry), nameof(BaseTroopType.LargeInfantry), nameof(BaseTroopType.Swarm), nameof(BaseTroopType.Shrine) };

    public int AttackBonus { get; set; }

    // Initiative modifier and conditions
    public int InitiativeModifier { get; set; }
    public bool InitOnCharge { get; set; }              // lances
    public bool InitWhenCharged { get; set; }           // spear/pike

    public int ArmourPiercing { get; set; }
    public bool AlwaysStrikesLast { get; set; }
    public bool RequiresTwoHands { get; set; }
    public int FightInExtraRanks { get; set; }
    public bool HasParry { get; set; }

    /// <summary>
    /// Get effective strength bonus for this weapon given combat context.
    /// </summary>
    public int GetStrengthBonus(bool isFirstRound, bool charged, bool wasCharged, string? defenderBaseType)
    {
        int bonus = 0;

        // Base or round-dependent strength
        if (HasRoundDependentStrength)
            bonus += isFirstRound ? StrengthBonusFirstRound : StrengthBonusSubsequent;
        else
            bonus += StrengthBonus;

        // Mighty Blow (conditional strength bonus)
        if (MightyBlow > 0)
        {
            bool mightyBlowApplies = false;
            if (MightyBlowOnCharge && charged)
                mightyBlowApplies = true;
            if (MightyBlowWhenCharged && wasCharged)
            {
                // Spear/pike Mighty Blow doesn't apply vs Infantry, Large Infantry, Swarms, Shrines
                if (defenderBaseType != null && !MightyBlowExcludedTypes.Contains(defenderBaseType))
                    mightyBlowApplies = true;
            }
            if (mightyBlowApplies)
                bonus += MightyBlow;
        }

        return bonus;
    }

    /// <summary>
    /// Get effective initiative modifier given combat context.
    /// </summary>
    public int GetInitiativeModifier(bool charged, bool wasCharged)
    {
        if (InitiativeModifier == 0) return 0;
        if (InitOnCharge) return charged ? InitiativeModifier : 0;
        if (InitWhenCharged) return wasCharged ? InitiativeModifier : 0;
        return InitiativeModifier; // unconditional
    }

    public string EffectSummary
    {
        get
        {
            var parts = new List<string>();
            if (HasRoundDependentStrength)
                parts.Add($"+{StrengthBonusFirstRound}S first round, +{StrengthBonusSubsequent}S after");
            else if (StrengthBonus != 0)
                parts.Add($"+{StrengthBonus}S");
            if (MightyBlow > 0)
            {
                var cond = MightyBlowOnCharge ? "on charge" : MightyBlowWhenCharged ? "when charged" : "";
                parts.Add($"Mighty Blow ({MightyBlow}) {cond}".Trim());
            }
            if (AttackBonus != 0) parts.Add($"+{AttackBonus}A");
            if (ArmourPiercing != 0) parts.Add($"AP{ArmourPiercing}");
            if (AlwaysStrikesLast) parts.Add("ASL");
            if (FightInExtraRanks > 0) parts.Add($"+{FightInExtraRanks} fighting rank(s)");
            if (HasParry) parts.Add("Parry");
            if (RequiresTwoHands) parts.Add("Two-handed");
            if (InitiativeModifier != 0)
            {
                var cond = InitOnCharge ? " on charge" : InitWhenCharged ? " when charged" : "";
                parts.Add($"+{InitiativeModifier}I{cond}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }
    }
}
