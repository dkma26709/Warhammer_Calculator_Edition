using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Services;

/// <summary>
/// One side's first-round numbers in a matchup, as the Matchup page states them.
/// </summary>
public sealed record EffectiveStats
{
    public int BaseContact { get; init; }
    public int TotalAttacks { get; init; }
    public int MountAttacks { get; init; }
    public bool HasMount { get; init; }
    public int EffectiveStrength { get; init; }
    public int EffectiveInitiative { get; init; }
    public int ToHitNeeded { get; init; }
    public RollRequirement ToWoundNeeded { get; init; }
    public bool HasPoison { get; init; }
    public int ArmourSave { get; init; }
    public int WardSave { get; init; }
    public int ImpactHits { get; init; }
    public bool IsCharging { get; init; }

    /// <summary>Strike order: higher strikes first, equal strikes simultaneously.</summary>
    public int StrikePriority { get; init; }

    /// <summary>
    /// The Matchup hero band's "This fight" phrase, without its prefix. Strike order is stated
    /// only against an opponent and when neither side has a mount.
    /// </summary>
    public string FightSummary(EffectiveStats? opponent)
    {
        var attacks = $"{TotalAttacks} attack{(TotalAttacks == 1 ? "" : "s")} at S{EffectiveStrength}";
        if (HasMount) attacks += $" (+{MountAttacks} mount)";

        var parts = new List<string>
        {
            $"{BaseContact} in base contact",
            attacks,
            $"hits on {ToHitNeeded}+",
            // Poison's to-wound step is not applied here yet, so "cannot wound" carries no poison tag.
            ToWoundNeeded.CanSucceed
                ? $"wounds on {ToWoundNeeded}{(HasPoison ? " (poison)" : "")}"
                : "cannot wound",
        };
        if (ImpactHits > 0) parts.Add($"{ImpactHits} impact hit{(ImpactHits == 1 ? "" : "s")}");
        if (ArmourSave <= 6) parts.Add($"{ArmourSave}+ armour");
        if (WardSave <= 6) parts.Add($"{WardSave}+ ward");

        // A mount strikes at its own Initiative, so one phrase can't state the order.
        var initiative = $"I{EffectiveInitiative}";
        if (opponent != null && !HasMount && !opponent.HasMount)
            initiative += $", {StrikeOrder(StrikePriority, opponent.StrikePriority)}";
        parts.Add(initiative);

        return string.Join(" · ", parts);
    }

    // Same rule as the engine: higher priority strikes first, equal priority simultaneously.
    private static string StrikeOrder(int mine, int theirs) =>
        mine > theirs ? "strikes first" : mine < theirs ? "strikes last" : "strikes simultaneously";
}

/// <summary>
/// Per-unit effective stats for a matchup, which the Matchup hero band states.
/// </summary>
public sealed class EffectiveStatsCalculator(IGameDataRepository repo)
{
    public EffectiveStats Compute(
        UnitConfiguration unit, UnitConfiguration opponent, ChargeState charge, bool isAttackerSide)
    {
        var context = new CombatContext { Charge = charge, CombatRound = 1 };
        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsHoldingAgainstCharge(isAttackerSide);

        // Weapon and weapon rules
        var weapon = CombatResolver.ResolveWeapon(unit, repo);

        // Effective initiative, as the engine's rider group strikes at it
        int effectiveI = CombatResolver.RiderInitiative(
            unit, weapon.Rules, isCharging, isBeingCharged, context.CombatRound);

        // Weapon Mighty Blow
        int weaponMB = 0;
        if (weapon.Rules.MightyBlow != null)
        {
            bool applyWeaponMB = ParsedWeaponRules.Holds(
                weapon.Rules.MightyBlowCondition, isCharging, isBeingCharged, context.CombatRound);
            if (applyWeaponMB)
                weaponMB = weapon.Rules.MightyBlow.Value;
        }

        // Effective strength
        int effectiveS = unit.EffectiveStrength(weapon, isCharging, context.CombatRound, weaponMB);

        // Total attacks
        int totalAttacks = unit.TotalAttacks(isCharging, context.CombatRound,
            weapon.Rules.ExtraAttacks, weapon.Rules.FightInExtraRanks ?? 0);
        int mountAttacks = unit.MountAttacks();

        // To-hit
        int attackerWS = unit.PrimaryProfile.WS ?? 1;
        int toHitNeeded = repo.GetToHitRequirement(attackerWS, opponent.BestWS).FirstRoll ?? 6;

        // To-wound
        var toWoundNeeded = repo.GetToWoundRequirement(effectiveS, opponent.BestT);

        // Armour save (this unit's save vs opponent's attacks)
        var opponentWeapon = CombatResolver.ResolveWeapon(opponent, repo);
        // Derive the opponent's flags from the context rather than mirroring this unit's — with
        // both sides charging neither is the negation of the other.
        bool opponentIsCharging = context.IsCharging(!isAttackerSide);
        bool opponentIsBeingCharged = context.IsHoldingAgainstCharge(!isAttackerSide);
        int opponentWeaponMB = 0;
        if (opponentWeapon.Rules.MightyBlow != null)
        {
            bool applyOppMB = ParsedWeaponRules.Holds(opponentWeapon.Rules.MightyBlowCondition,
                opponentIsCharging, opponentIsBeingCharged, context.CombatRound);
            if (applyOppMB)
                opponentWeaponMB = opponentWeapon.Rules.MightyBlow.Value;
        }
        int opponentS = opponent.EffectiveStrength(
            opponentWeapon, opponentIsCharging, context.CombatRound, opponentWeaponMB);
        var opponentRules = opponent.EffectiveSpecialRules;
        int opponentAP = opponentRules.ArmourPiercing ?? 0;
        int strengthMod = repo.GetArmourSaveModifier(opponentS);
        int armourSave = unit.EffectiveArmourSave(opponentS, opponentAP, strengthMod);

        // Ward save (best of ward/regen/parry)
        var unitRules = unit.EffectiveSpecialRules;
        int wardValue = unitRules.WardSave ?? 7;
        int regenValue = unitRules.Regeneration ?? 7;
        int parryValue = unitRules.Parry ?? 7;
        int bestWard = Math.Min(Math.Min(wardValue, regenValue), parryValue);

        // Impact hits — must agree with AttackResolver.GetImpactHitCount, which is what the
        // simulation actually rolls. The troop-type contribution is per-system: wap-v2 gives
        // Cavalry and Monstrous Cavalry one each, wap-v3 gives them none.
        int impactHits = 0;
        if (isCharging)
        {
            int fromRules = unitRules.ImpactHits.HasValue
                ? unitRules.ImpactHits.Value * unit.ModelsInFrontRank
                : 0;
            int fromType = unit.TroopType.InherentImpactHits * unit.ModelsInFrontRank;
            impactHits = Math.Max(fromRules, fromType);
        }

        // Base contact
        int baseContact = BaseContactCalculator.ModelsInBaseContact(unit, opponent);

        // Strike order, by the formula the engine sorts its attack groups with
        int strikePriority = CombatResolver.StrikePriority(
            effectiveI, unitRules.AlwaysStrikesFirst, unitRules.AlwaysStrikesLast);

        return new EffectiveStats
        {
            BaseContact = baseContact,
            TotalAttacks = totalAttacks,
            MountAttacks = mountAttacks,
            HasMount = unit.MountProfile != null && unit.TroopType.HasSplitProfile,
            EffectiveStrength = effectiveS,
            EffectiveInitiative = effectiveI,
            ToHitNeeded = toHitNeeded,
            ToWoundNeeded = toWoundNeeded,
            HasPoison = unitRules.PoisonedAttacks,
            ArmourSave = armourSave,
            WardSave = bestWard,
            ImpactHits = impactHits,
            IsCharging = isCharging,
            StrikePriority = strikePriority
        };
    }
}
