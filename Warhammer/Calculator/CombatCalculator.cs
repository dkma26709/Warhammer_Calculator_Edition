namespace Warhammer.Calculator;

public class CombatCalculator
{
    public DiceSimulator DiceSimulator { get; set; }
    
    public CombatCalculator()
    {
        DiceSimulator = new DiceSimulator();
    }

    public CombatResult CalculateCombatRound(IUnit attacker, IUnit defender)
    {
        var rng = new Random();

        var attackerToHit = 4;
        var attackerToWound = 3;
        var defdenderArmorSave = 5;
        var defenderWardSave = 6;
        
        var attackerAttacks = attacker.Attacks*5 + 5;
        var attackRolls = DiceSimulator.SimulateD6Rolls(attackerAttacks);
        
        var successfulHits = DiceSimulator.GetD6RollsWithinRange(attackRolls, attackerToHit, 6);
        
        var woundRolls = DiceSimulator.SimulateD6Rolls(successfulHits.Count);
        var successfulWounds = DiceSimulator.GetD6RollsWithinRange(woundRolls, attackerToWound, 6);
        
        var armorSaveRolls = DiceSimulator.SimulateD6Rolls(successfulWounds.Count);
        var succesfulArmorSaves = DiceSimulator.GetD6RollsWithinRange(armorSaveRolls, defdenderArmorSave);
        var failedArmorSaves = successfulWounds.Count - succesfulArmorSaves.Count;
        
        var wardSaveRolls = DiceSimulator.SimulateD6Rolls(failedArmorSaves);
        var succesFulWardSaves = DiceSimulator.GetD6RollsWithinRange(wardSaveRolls, defenderWardSave);

        var damageDealt = wardSaveRolls.Count - succesFulWardSaves.Count;

        return new CombatResult
        {
            Hits = successfulHits.Count,
            Wounds = successfulWounds.Count,
            WoundsDealtAfterArmorSaves = failedArmorSaves,
            WoundsDealtAfterWardSaves = damageDealt,
        };
    }
    
    public List<CombatResult> SimulateCombatRounds(IUnit attacker, IUnit defender, int rounds)
    {
        var results = new List<CombatResult>();
        for (int i = 0; i < rounds; i++)
        {
            var result = CalculateCombatRound(attacker, defender);
            results.Add(result);
        }
        return results;
    }
    
    public List<PercentageRoll> CalculateCombatResultsPercentage(List<CombatResult> results)
    {
        if (results.Count == 0)
        {
            return new List<PercentageRoll>();
        }

        var totalResults = results.Count;
        var percentageResults = new List<PercentageRoll>();
        
        var damageGroups = results.GroupBy(x => x.WoundsDealtAfterWardSaves)
            .Select(g => new { WoundsDealt = g.Key, Count = g.Count() })
            .OrderBy(x => x.WoundsDealt);
        
        foreach (var group in damageGroups)
        {
            var percentage = (decimal)group.Count / totalResults * 100;
            percentageResults.Add(new PercentageRoll
            {
                RollValue = group.WoundsDealt,
                Count = group.Count,
                Percentage = percentage
            });
        }

        return percentageResults;
    }
}

public class CombatResult
{
    public int Hits { get; set; }
    public int Wounds { get; set; }
    public int WoundsDealtAfterArmorSaves { get; set; }
    public int WoundsDealtAfterWardSaves { get; set; }
}
