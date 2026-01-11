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
        var attackerToHit = GetToHitValue(attacker, defender);
        var attackerToWound = GetToWoundValue(attacker, defender);
        var defenderArmorSave = GetArmorSaveValue(defender);
        var defenderWardSave = GetWardSaveValue(defender);
        
        var attackerAttacks = attacker.Attacks*5 + 5;
        var attackRolls = DiceSimulator.SimulateD6Rolls(attackerAttacks);
        
        var successfulHits = DiceSimulator.GetD6RollsWithinRange(attackRolls, attackerToHit, 6);
        
        var woundRolls = DiceSimulator.SimulateD6Rolls(successfulHits.Count);
        var successfulWounds = DiceSimulator.GetD6RollsWithinRange(woundRolls, attackerToWound, 6);
        
        var armorSaveRolls = DiceSimulator.SimulateD6Rolls(successfulWounds.Count);
        var succesfulArmorSaves = DiceSimulator.GetD6RollsWithinRange(armorSaveRolls, defenderArmorSave);
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

    private int GetWardSaveValue(IUnit defender)
    {
        if(defender.WardSave is null && defender.RegenerationSave is null)
        {
            return 7; // Impossible save
        }
        
        if(defender.WardSave is not null)
        {
            return defender.WardSave.Value;
        }
        
        if(defender.RegenerationSave is not null)
        {
            return defender.RegenerationSave.Value;
        }

        if (defender.WardSave.Value < 4 || defender.RegenerationSave.Value < 4)
        {
            return Math.Min(defender.WardSave.Value, defender.RegenerationSave.Value);
        }
            
        return Math.Max(defender.WardSave.Value - defender.RegenerationSave.Value, 4);
    }

    private int GetArmorSaveValue(IUnit defender)
    {
        if(defender.ArmorSave is null)
        {
            return 7; // Impossible save
        }
        
        return Math.Max(defender.ArmorSave.Value, 2);
    }

    private int GetToWoundValue(IUnit attacker, IUnit defender)
    {
        var strentgTougnessDiff = attacker.Strength - defender.Toughness;
        var toWoundValue = strentgTougnessDiff switch
        {
            0 => 4,
            1 => 3,
            -1 => 5,
            >= 2 => 2,
            <= -2 => 6,
        };
        return toWoundValue;

    }

    private int GetToHitValue(IUnit attacker, IUnit defender)
    {
        if (attacker.WeaponSkill == defender.WeaponSkill)
        {
            return 4;
        }

        if (attacker.WeaponSkill*2 > defender.WeaponSkill)
        {
            return 2;
        }

        if (attacker.WeaponSkill < defender.WeaponSkill*2)
        {
            return 6;
        }
        
        if (attacker.WeaponSkill > defender.WeaponSkill)
        {
            return 3;
        }
        
        return 5;
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
