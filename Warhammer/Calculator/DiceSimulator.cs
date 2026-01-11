namespace Warhammer.Calculator;

public class DiceSimulator
{
    private Random Rand { get; set; } = new Random();
    public int D6Roll()
    {
        return Rand.Next(1,7); // Generates a number between 1 and 6 - upper bound is exclusive
    }

    public int D3Roll()
    {
        return Rand.Next(1,4); // Generates a number between 1 and 3 - upper bound is exclusive
    }
    
    public List<int> SimulateD6Rolls(int rolls)
    {
        var result = new List<int>();
        for (int i = 0; i < rolls; i++)
        {
            result.Add(D6Roll());

        }
        return result;
    }

    public List<int> GetD6RollsWithinRange(List<int> rolls, int minValue = 1, int maxValue = 6)
    {
        return rolls.Where(x => x >= minValue && x <= maxValue).ToList();
    }
    
    public List<PercentageRoll> CalculateD6PercentageRolls(List<int> rolls)
    {
        if (rolls.Count == 0)
        {
            return new List<PercentageRoll>();
        }

        var totalRolls = rolls.Count;
        var percentageRolls = new List<PercentageRoll>();
        
        for (int i = 1; i <= 6; i++)
        {
            var count = rolls.Count(x => x == i);
            var percentage = (decimal)count / totalRolls * 100;
            percentageRolls.Add(new PercentageRoll
            {
                RollValue = i,
                Count = count,
                Percentage = percentage
            });
        }

        return percentageRolls;
    }
}

public class PercentageRoll
{
    public int RollValue { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public interface IUnit
{
    public UnitType UnitType { get; set; }
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
    public int? ArmorSave { get; set; }
    public int? WardSave { get; set; }
    public int? RegenerationSave { get; set; }
    
    public List<SpecialRule> SpecialRules { get; set; }
    public List<Weapon> Weapons { get; set; }
    public List<RangedWeapon> RangedWeapons { get; set; }
    public List<Armor> Armors { get; set; }
}

public enum UnitType
{
    Infantry,
    Cavalry,
}

public enum SpecialRule
{
    PredatoryFighter
}

public class Weapon
{
    public string Name { get; set; }
    public int Strength { get; set; }
    public int Damage { get; set; }
    public List<SpecialRule> SpecialRules { get; set; }
}

public class RangedWeapon
{
    public string Name { get; set; }
    public int Strength { get; set; }
    public int Range { get; set; }
    public int Damage { get; set; }
    public List<SpecialRule> SpecialRules { get; set; }
}

public class Armor
{
    public string Name { get; set; }
    public int Save { get; set; }
    public List<SpecialRule> SpecialRules { get; set; }
}

public class InfantryUnit : IUnit
{
    public UnitType UnitType { get; set; } = UnitType.Infantry;
    public required int Movement { get; set; }
    public required int WeaponSkill { get; set; }
    public required int BallisticSkill { get; set; }
    public required int Strength { get; set; }
    public required int Toughness { get; set; }
    public required int Wounds { get; set; }
    public required int Initiative { get; set; }
    public required int Attacks { get; set; }
    public required int Leadership { get; set; }
    public int? ArmorSave { get; set; }
    public int? WardSave { get; set; }
    public int? RegenerationSave { get; set; }

    public List<SpecialRule> SpecialRules { get; set; } = new List<SpecialRule>();
    public List<Weapon> Weapons { get; set; } = new List<Weapon>();
    public List<RangedWeapon> RangedWeapons { get; set; } = new List<RangedWeapon>();
    public List<Armor> Armors { get; set; } = new List<Armor>();
}