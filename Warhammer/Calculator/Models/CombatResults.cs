namespace Warhammer.Calculator.Models;

public record CombatSimulationResult
{
    public int Iterations { get; init; }
    public double AttackerWinPct { get; init; }
    public double DefenderWinPct { get; init; }
    public double DrawPct { get; init; }
    public double AvgAttackerWounds { get; init; }
    public double AvgDefenderWounds { get; init; }
    public double AvgAttackerCombatRes { get; init; }
    public double AvgDefenderCombatRes { get; init; }
    public CombatResBreakdown AvgAttackerBreakdown { get; init; } = new();
    public CombatResBreakdown AvgDefenderBreakdown { get; init; } = new();
    public List<DistributionEntry> Distribution { get; init; } = [];
    public List<WoundDistributionEntry> AttackerWoundDistribution { get; init; } = [];
    public List<WoundDistributionEntry> DefenderWoundDistribution { get; init; } = [];
}

public record CombatResBreakdown
{
    public double Wounds { get; init; }
    public double RankBonus { get; init; }
    public double StandardBearer { get; init; }
    public double ChargeBonus { get; init; }
    public double FearBonus { get; init; }
    public double MusicianBonus { get; init; }
}

public record DistributionEntry
{
    public int CombatResDifference { get; init; }
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public record RoundResult
{
    public int AttackerWoundsDealt { get; init; }
    public int DefenderWoundsDealt { get; init; }
    public int AttackerCombatRes { get; init; }
    public int DefenderCombatRes { get; init; }
    public CombatResBreakdown AttackerBreakdown { get; init; } = new();
    public CombatResBreakdown DefenderBreakdown { get; init; } = new();
}

public record WoundDistributionEntry
{
    public int Wounds { get; init; }
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public record AttackResult
{
    public int TotalHits { get; init; }
    public int TotalWoundsInflicted { get; init; }
    public int KillingBlowWounds { get; init; }
    public int UnsavedWounds { get; init; }
}
