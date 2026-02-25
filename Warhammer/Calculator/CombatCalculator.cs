namespace Warhammer.Calculator;

using Warhammer.Models;
using Warhammer.Services;

public class CombatCalculator
{
    private readonly DiceSimulator _dice = new();

    public RankedCombatResult SimulateRound(
        UnitConfiguration unitA,
        UnitConfiguration unitB,
        bool unitACharged = false,
        bool unitBCharged = false)
    {
        var attacksA = unitA.CalculateAttacks();
        var attacksB = unitB.CalculateAttacks();

        // Resolve unit A's model attacks against unit B
        var damageAtoB = ResolveAttacks(
            unitA.PrimaryProfile.WS ?? 1,
            unitA.EffectiveStrength,
            unitA.SelectedWeapon?.ArmourPiercing ?? 0,
            attacksA.TotalModelAttacks,
            unitB);

        // Resolve unit A's mount attacks against unit B
        if (attacksA.MountAttacks > 0 && unitA.MountProfile != null)
        {
            var mountDamage = ResolveAttacks(
                unitA.MountProfile.WS ?? 1,
                unitA.MountProfile.S ?? 3,
                0,
                attacksA.MountAttacks,
                unitB);
            damageAtoB.MountWoundsDealt = mountDamage.WoundsAfterSaves;
        }

        // Resolve unit B's model attacks against unit A
        var damageBtoA = ResolveAttacks(
            unitB.PrimaryProfile.WS ?? 1,
            unitB.EffectiveStrength,
            unitB.SelectedWeapon?.ArmourPiercing ?? 0,
            attacksB.TotalModelAttacks,
            unitA);

        // Resolve unit B's mount attacks against unit A
        if (attacksB.MountAttacks > 0 && unitB.MountProfile != null)
        {
            var mountDamage = ResolveAttacks(
                unitB.MountProfile.WS ?? 1,
                unitB.MountProfile.S ?? 3,
                0,
                attacksB.MountAttacks,
                unitA);
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
        int armourPiercing,
        int numberOfAttacks,
        UnitConfiguration defender)
    {
        var result = new AttackResult { TotalAttacks = numberOfAttacks };

        int toHit = CoreRulesService.GetToHitValue(attackerWS, defender.PrimaryProfile.WS ?? 1);
        int toWound = CoreRulesService.GetToWoundValue(strength, defender.PrimaryProfile.T ?? 1);

        // Roll to hit
        var hitRolls = _dice.SimulateD6Rolls(numberOfAttacks);
        result.Hits = hitRolls.Count(r => r >= toHit);

        // Roll to wound
        var woundRolls = _dice.SimulateD6Rolls(result.Hits);
        result.Wounds = woundRolls.Count(r => r >= toWound);

        // Armour saves
        int armourSaveNeeded = defender.CalculateArmourSave(strength, armourPiercing);
        if (armourSaveNeeded <= 6)
        {
            var saveRolls = _dice.SimulateD6Rolls(result.Wounds);
            result.ArmorSavesPassed = saveRolls.Count(r => r >= armourSaveNeeded);
        }

        result.WoundsAfterSaves = result.Wounds - result.ArmorSavesPassed;
        return result;
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
