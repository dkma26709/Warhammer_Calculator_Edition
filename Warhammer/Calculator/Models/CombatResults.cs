namespace Warhammer.Calculator.Models;

public record CombatSimulationResult
{
    public int Iterations { get; init; }
    public long ElapsedMs { get; init; }
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

public struct RoundResult
{
    public int AttackerWoundsDealt;
    public int DefenderWoundsDealt;
    public int AttackerCombatRes;
    public int DefenderCombatRes;
    public MutableBreakdown AttackerBreakdown;
    public MutableBreakdown DefenderBreakdown;
}

public struct MutableBreakdown
{
    public int Wounds;
    public int RankBonus;
    public int StandardBearer;
    public int ChargeBonus;
    public double FearBonus;
    public int MusicianBonus;

    public readonly int Total() =>
        Wounds + RankBonus + StandardBearer + ChargeBonus + (int)FearBonus + MusicianBonus;
}

public record WoundDistributionEntry
{
    public int Wounds { get; init; }
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public struct AttackResult
{
    public int TotalHits;
    public int TotalWoundsInflicted;
    public int KillingBlowWounds;
    public int UnsavedWounds;
}
