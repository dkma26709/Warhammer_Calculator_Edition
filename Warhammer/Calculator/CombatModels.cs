namespace Warhammer.Calculator;

using Warhammer.Models;

public class AttackResult
{
    public int TotalAttacks { get; set; }
    public int Hits { get; set; }
    public int Wounds { get; set; }
    public int ArmorSavesPassed { get; set; }
    public int KillingBlowWounds { get; set; }
    public int WardSavesPassed { get; set; }
    public int WoundsAfterSaves { get; set; }
    public int MountWoundsDealt { get; set; }

    public int TotalWounds => WoundsAfterSaves + MountWoundsDealt;
}

public class RankedCombatResult
{
    public string UnitAName { get; set; } = "";
    public string UnitBName { get; set; } = "";
    public CombatAttackBreakdown UnitAAttackBreakdown { get; set; } = new();
    public CombatAttackBreakdown UnitBAttackBreakdown { get; set; } = new();
    public AttackResult DamageAToB { get; set; } = new();
    public AttackResult DamageBToA { get; set; } = new();
    public int CombatResolutionA { get; set; }
    public int CombatResolutionB { get; set; }
    public CombatWinner Winner { get; set; }
    public int CombatResolutionDifference { get; set; }
}

public enum CombatWinner { UnitA, UnitB, Draw }

public class CombatSimulationSummary
{
    public int TotalSimulations { get; set; }
    public decimal UnitAWinPercentage { get; set; }
    public decimal UnitBWinPercentage { get; set; }
    public decimal DrawPercentage { get; set; }
    public decimal AverageWoundsAtoB { get; set; }
    public decimal AverageWoundsBtoA { get; set; }
    public decimal AverageCombatResA { get; set; }
    public decimal AverageCombatResB { get; set; }
    public List<DistributionEntry> WoundsAToBDistribution { get; set; } = new();
    public List<DistributionEntry> WoundsBToADistribution { get; set; } = new();
    public List<DistributionEntry> CombatResDiffDistribution { get; set; } = new();
}

public class DistributionEntry
{
    public int Value { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
