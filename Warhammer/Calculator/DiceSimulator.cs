namespace Warhammer.Calculator;

public class DiceSimulator
{
    public List<int> SimulateD6Rolls(int rolls = 1000)
    {
        var rand = new Random();
        var result = new List<int>();
        for (int i = 0; i < rolls; i++)
        {
            result.Add(rand.Next(1,6));
           
        }
        return result;
    }

    public List<int> GetRollsWithinRange(List<int> rolls, int minValue = 1, int maxValue = 6)
    {
        return rolls.Where(x => x >= minValue && x <= maxValue).ToList();
    }
    
    public List<PercentageRolls> CalculatePercentageRolls(List<int> rolls)
    {
        if (rolls.Count == 0)
        {
            return new List<PercentageRolls>();
        }

        var totalRolls = rolls.Count;
        var percentageRolls = new List<PercentageRolls>();
        
        for (int i = 1; i <= 6; i++)
        {
            var count = rolls.Count(x => x == i);
            var percentage = (double)count / totalRolls * 100;
            percentageRolls.Add(new PercentageRolls
            {
                RollValue = i,
                Count = count,
                Percentage = percentage
            });
        }

        return percentageRolls;
    }
}

public class PercentageRolls
{
    public int RollValue { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public interface IUnit
{
    public UnitType UnitType { get; set; }
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
    
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