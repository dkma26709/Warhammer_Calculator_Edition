namespace Warhammer.Calculator;

using Warhammer.Models;
using Warhammer.Services;

public class CombatCalculator
{
    private readonly DiceSimulator _dice = new();
    private readonly CoreRulesService _rules;

    public CombatCalculator(CoreRulesService rules)
    {
        _rules = rules;
    }

    public RankedCombatResult SimulateRound(
        UnitConfiguration unitA,
        UnitConfiguration unitB,
        bool unitACharged = false,
        bool unitBCharged = false)
    {
        var attacksA = unitA.CalculateAttacks(unitB);
        var attacksB = unitB.CalculateAttacks(unitA);

        // Context: if unitA charged, unitA gets charge bonuses; unitB gets "was charged" bonuses
        bool isFirstRound = true; // single-round simulation

        string? defenderBBaseType = unitB.TroopType?.BaseType.ToString();
        string? defenderABaseType = unitA.TroopType?.BaseType.ToString();

        int strengthA = unitA.GetEffectiveStrength(isFirstRound, unitACharged, unitBCharged, defenderBBaseType);
        int strengthB = unitB.GetEffectiveStrength(isFirstRound, unitBCharged, unitACharged, defenderABaseType);

        // Resolve unit A's model attacks against unit B
        var damageAtoB = ResolveAttacks(
            unitA.PrimaryProfile.WS ?? 1,
            strengthA,
            unitA.SelectedWeapon?.ArmourPiercing ?? 0,
            attacksA.TotalModelAttacks,
            unitA, unitB, isFirstRound);

        // Resolve unit A's mount attacks against unit B (mounts don't get unit special rules for attacks)
        if (attacksA.MountAttacks > 0 && unitA.MountProfile != null)
        {
            var mountRules = new ParsedSpecialRules(); // mounts use basic attacks
            var mountDamage = ResolveAttacks(
                unitA.MountProfile.WS ?? 1,
                unitA.MountProfile.S ?? 3,
                0,
                attacksA.MountAttacks,
                unitA, unitB, isFirstRound, mountRules);
            damageAtoB.MountWoundsDealt = mountDamage.WoundsAfterSaves;
        }

        // Resolve unit B's model attacks against unit A
        var damageBtoA = ResolveAttacks(
            unitB.PrimaryProfile.WS ?? 1,
            strengthB,
            unitB.SelectedWeapon?.ArmourPiercing ?? 0,
            attacksB.TotalModelAttacks,
            unitB, unitA, isFirstRound);

        // Resolve unit B's mount attacks against unit A
        if (attacksB.MountAttacks > 0 && unitB.MountProfile != null)
        {
            var mountRules = new ParsedSpecialRules();
            var mountDamage = ResolveAttacks(
                unitB.MountProfile.WS ?? 1,
                unitB.MountProfile.S ?? 3,
                0,
                attacksB.MountAttacks,
                unitB, unitA, isFirstRound, mountRules);
            damageBtoA.MountWoundsDealt = mountDamage.WoundsAfterSaves;
        }

        // Combat Resolution
        int crA = CalculateCombatResolution(damageAtoB.TotalWounds, unitA, unitACharged);
        int crB = CalculateCombatResolution(damageBtoA.TotalWounds, unitB, unitBCharged);

        return new RankedCombatResult
        {
            UnitAName = unitA.SourceUnit.Name,
            UnitBName = unitB.SourceUnit.Name,
            UnitAAttackBreakdown = attacksA,
            UnitBAttackBreakdown = attacksB,
            DamageAToB = damageAtoB,
            DamageBToA = damageBtoA,
            CombatResolutionA = crA,
            CombatResolutionB = crB,
            Winner = crA > crB ? CombatWinner.UnitA
                   : crB > crA ? CombatWinner.UnitB
                   : CombatWinner.Draw,
            CombatResolutionDifference = Math.Abs(crA - crB),
        };
    }

    private AttackResult ResolveAttacks(
        int attackerWS,
        int strength,
        int weaponArmourPiercing,
        int numberOfAttacks,
        UnitConfiguration attacker,
        UnitConfiguration defender,
        bool isFirstRound,
        ParsedSpecialRules? overrideRules = null)
    {
        var result = new AttackResult { TotalAttacks = numberOfAttacks };
        var attackerRules = overrideRules ?? attacker.SpecialRules;

        int toHit = _rules.GetToHitValue(attackerWS, defender.PrimaryProfile.WS ?? 1);
        int toWound = _rules.GetToWoundValue(strength, defender.PrimaryProfile.T ?? 1);

        // Poisoned Attacks: +1 to wound modifier
        if (attackerRules.PoisonedAttacks)
            toWound = Math.Max(toWound - 1, 2);

        // Total armour piercing: weapon + unit special rule
        int totalAP = weaponArmourPiercing + attackerRules.ArmourPiercing;

        // Roll to hit
        var hitRolls = _dice.SimulateD6Rolls(numberOfAttacks);
        result.Hits = hitRolls.Count(r => r >= toHit);

        // Hatred: re-roll failed to-hit in first round
        if (attackerRules.Hatred && isFirstRound)
        {
            int misses = numberOfAttacks - result.Hits;
            if (misses > 0)
            {
                var rerolls = _dice.SimulateD6Rolls(misses);
                result.Hits += rerolls.Count(r => r >= toHit);
            }
        }

        // Roll to wound
        var woundRolls = _dice.SimulateD6Rolls(result.Hits);
        int normalWounds = woundRolls.Count(r => r >= toWound);
        result.Wounds = normalWounds;

        // Killing Blow: natural 6 to wound
        int killingBlowWounds = 0;
        bool hasKB = attackerRules.KillingBlow || attackerRules.HeroicKillingBlow;
        if (hasKB)
        {
            int kbCount = woundRolls.Count(r => r == 6);
            if (kbCount > 0 && IsKillingBlowEffective(attackerRules, defender))
            {
                killingBlowWounds = kbCount;
                normalWounds -= killingBlowWounds;
            }
            // Against non-eligible types, KB wounds on 6 still count as normal wounds
            // that ignore armour + regen (handled below)
        }
        result.KillingBlowWounds = killingBlowWounds;

        // Armour saves — only for non-KB wounds (KB ignores armour)
        int armourSaveNeeded = defender.CalculateArmourSave(strength, totalAP, _rules);
        int armourSaved = 0;
        if (armourSaveNeeded <= 6 && normalWounds > 0)
        {
            var saveRolls = _dice.SimulateD6Rolls(normalWounds);
            armourSaved = saveRolls.Count(r => r >= armourSaveNeeded);
        }
        result.ArmorSavesPassed = armourSaved;

        int woundsAfterArmour = (normalWounds - armourSaved) + killingBlowWounds;

        // Ward Save / Regeneration
        // KB wounds: only Ward Save (Regen negated by KB)
        // Flaming attack wounds: only Ward Save (Regen negated by Flaming)
        // Normal wounds: best of Ward Save and Regeneration
        int wardValue = defender.SpecialRules.WardSave ?? 7;
        int regenValue = defender.SpecialRules.Regeneration ?? 7;

        int wardOnlyWounds = killingBlowWounds; // KB wounds skip regen
        int fullSaveWounds = normalWounds - armourSaved;

        // Flaming attacks negate all Regeneration
        if (attackerRules.FlamingAttacks)
        {
            wardOnlyWounds += fullSaveWounds;
            fullSaveWounds = 0;
        }

        int totalAfterSaved = 0;

        // Ward-only saves (KB/Flaming wounds)
        if (wardOnlyWounds > 0 && wardValue <= 6)
        {
            var wardRolls = _dice.SimulateD6Rolls(wardOnlyWounds);
            totalAfterSaved += wardRolls.Count(r => r >= wardValue);
        }

        // Full saves (best of ward/regen)
        if (fullSaveWounds > 0)
        {
            int bestSave = Math.Min(wardValue, regenValue);
            if (bestSave <= 6)
            {
                var saveRolls = _dice.SimulateD6Rolls(fullSaveWounds);
                totalAfterSaved += saveRolls.Count(r => r >= bestSave);
            }
        }

        result.WardSavesPassed = totalAfterSaved;
        result.WoundsAfterSaves = woundsAfterArmour - totalAfterSaved;

        return result;
    }

    private static bool IsKillingBlowEffective(ParsedSpecialRules attackerRules, UnitConfiguration defender)
    {
        var defenderType = defender.TroopType?.BaseType ?? BaseTroopType.Unknown;

        if (attackerRules.HeroicKillingBlow)
        {
            // Heroic KB works against all types except Swarm
            return defenderType != BaseTroopType.Swarm;
        }

        // Normal KB: only Infantry, Cavalry, War Beasts
        return defenderType is BaseTroopType.Infantry or BaseTroopType.Cavalry or BaseTroopType.WarBeast;
    }

    private int CalculateCombatResolution(int woundsDealt, UnitConfiguration unit, bool charged)
    {
        int cr = woundsDealt;
        cr += unit.RankBonus;
        if (unit.HasStandardBearer) cr += 1;
        if (charged) cr += 1;
        return cr;
    }

    public List<RankedCombatResult> SimulateMultiple(
        UnitConfiguration unitA,
        UnitConfiguration unitB,
        int iterations,
        bool unitACharged = false,
        bool unitBCharged = false)
    {
        var results = new List<RankedCombatResult>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            results.Add(SimulateRound(unitA, unitB, unitACharged, unitBCharged));
        }
        return results;
    }

    public CombatSimulationSummary Summarize(List<RankedCombatResult> results)
    {
        if (results.Count == 0)
            return new CombatSimulationSummary();

        return new CombatSimulationSummary
        {
            TotalSimulations = results.Count,
            UnitAWinPercentage = Math.Round((decimal)results.Count(r => r.Winner == CombatWinner.UnitA) / results.Count * 100, 1),
            UnitBWinPercentage = Math.Round((decimal)results.Count(r => r.Winner == CombatWinner.UnitB) / results.Count * 100, 1),
            DrawPercentage = Math.Round((decimal)results.Count(r => r.Winner == CombatWinner.Draw) / results.Count * 100, 1),
            AverageWoundsAtoB = Math.Round((decimal)results.Average(r => r.DamageAToB.TotalWounds), 2),
            AverageWoundsBtoA = Math.Round((decimal)results.Average(r => r.DamageBToA.TotalWounds), 2),
            AverageCombatResA = Math.Round((decimal)results.Average(r => r.CombatResolutionA), 2),
            AverageCombatResB = Math.Round((decimal)results.Average(r => r.CombatResolutionB), 2),
            WoundsAToBDistribution = BuildDistribution(
                results.Select(r => r.DamageAToB.TotalWounds)),
            WoundsBToADistribution = BuildDistribution(
                results.Select(r => r.DamageBToA.TotalWounds)),
            CombatResDiffDistribution = BuildDistribution(
                results.Select(r => r.CombatResolutionA - r.CombatResolutionB)),
        };
    }

    private static List<DistributionEntry> BuildDistribution(IEnumerable<int> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return new();

        return list.GroupBy(v => v)
            .Select(g => new DistributionEntry
            {
                Value = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((decimal)g.Count() / list.Count * 100, 1)
            })
            .OrderBy(p => p.Value)
            .ToList();
    }
}
