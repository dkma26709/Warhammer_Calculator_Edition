namespace Warhammer.Calculator.Models;

public enum CombatOutcome
{
    AttackerWipedOut,
    DefenderWipedOut,
    BothWipedOut,
    AttackerBroke,
    DefenderBroke,
    AttackerBrokeCaught,
    DefenderBrokeCaught,
    AttackerBrokeEscaped,
    DefenderBrokeEscaped,
    MaxRoundsReached
}

public record MultiRoundSimulationResult
{
    public int Iterations { get; init; }
    public long ElapsedMs { get; init; }
    public int MaxRounds { get; init; }
    public double AvgRoundsPlayed { get; init; }
    public Dictionary<CombatOutcome, double> OutcomePcts { get; init; } = new();
    public List<RoundSummary> RoundSummaries { get; init; } = [];
}

/// <summary>
/// Full stats for a single combat round, averaged across all iterations that reached it.
/// </summary>
public record RoundSummary
{
    public int Round { get; init; }
    public int IterationsReached { get; init; }
    public double PctStillFighting { get; init; }
    public double AvgAttackerModelsStart { get; init; }
    public double AvgDefenderModelsStart { get; init; }
    public double AvgAttackerModelsRemaining { get; init; }
    public double AvgDefenderModelsRemaining { get; init; }

    // Per-round combat stats (same shape as CombatSimulationResult)
    public double AttackerWinPct { get; init; }
    public double DefenderWinPct { get; init; }
    public double DrawPct { get; init; }
    public double AvgAttackerWounds { get; init; }
    public double AvgDefenderWounds { get; init; }
    public double AvgAttackerCombatRes { get; init; }
    public double AvgDefenderCombatRes { get; init; }
    public CombatResBreakdown AvgAttackerBreakdown { get; init; } = new();
    public CombatResBreakdown AvgDefenderBreakdown { get; init; } = new();

    // Break test stats (% of total iterations)
    public double AttackerTestedPct { get; init; }
    public double AttackerFledPct { get; init; }
    public double DefenderTestedPct { get; init; }
    public double DefenderFledPct { get; init; }

    // Distributions (same shape as CombatSimulationResult)
    public List<DistributionEntry> Distribution { get; init; } = [];
    public List<WoundDistributionEntry> AttackerWoundDistribution { get; init; } = [];
    public List<WoundDistributionEntry> DefenderWoundDistribution { get; init; } = [];
}
