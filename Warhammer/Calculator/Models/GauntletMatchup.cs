using Warhammer.Models.Domain;

namespace Warhammer.Calculator.Models;

/// <summary>
/// Result of a single matchup within a gauntlet run.
/// Wraps the opponent info and either a single-round or multi-round simulation result.
/// </summary>
public record GauntletMatchup(
    Unit Opponent,
    UnitConfiguration OpponentConfig,
    string? OpponentFactionKey,
    CombatSimulationResult? SingleRound,
    MultiRoundSimulationResult? MultiRound)
{
    public double WinPct => SingleRound?.AttackerWinPct
        ?? MultiRound?.OutcomePcts
            .Where(kv => kv.Key is CombatOutcome.DefenderWipedOut
                or CombatOutcome.DefenderBrokeCaught
                or CombatOutcome.DefenderBrokeEscaped)
            .Sum(kv => kv.Value)
        ?? 0;

    public double LossPct => SingleRound?.DefenderWinPct
        ?? MultiRound?.OutcomePcts
            .Where(kv => kv.Key is CombatOutcome.AttackerWipedOut
                or CombatOutcome.AttackerBrokeCaught
                or CombatOutcome.AttackerBrokeEscaped)
            .Sum(kv => kv.Value)
        ?? 0;

    public double DrawPct => SingleRound?.DrawPct
        ?? MultiRound?.OutcomePcts.GetValueOrDefault(CombatOutcome.MaxRoundsReached)
        ?? 0;

    public double AvgWoundsDealt => SingleRound?.AvgAttackerWounds
        ?? MultiRound?.RoundSummaries.FirstOrDefault()?.AvgAttackerWounds ?? 0;

    public double AvgWoundsTaken => SingleRound?.AvgDefenderWounds
        ?? MultiRound?.RoundSummaries.FirstOrDefault()?.AvgDefenderWounds ?? 0;
}
