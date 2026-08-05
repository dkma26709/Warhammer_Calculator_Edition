using Warhammer.Models.Domain;

namespace Warhammer.Calculator.Models;

/// <summary>
/// Result of a single matchup within a gauntlet run.
/// Wraps the opponent info and either a single-round or multi-round simulation result.
/// </summary>
/// <remarks>
/// Win/draw/loss here is <b>combat resolution</b>, read from the first round of combat, and
/// is deliberately independent of the round cap. The multi-round outcome buckets are not
/// usable for this: a unit that can neither be broken nor wiped out inside the cap — anything
/// Unstable or Stubborn — lands in <see cref="CombatOutcome.MaxRoundsReached"/> however far
/// ahead the subject was, so the same matchup swung fifty points of "win rate" on the cap
/// alone. Round one always runs the full iteration count with both units at full strength
/// (see the per-round iteration rule), so it needs no active-only denominator.
/// The full outcome distribution is still available in the expanded detail panel.
/// </remarks>
public record GauntletMatchup(
    Unit Opponent,
    UnitConfiguration OpponentConfig,
    string? OpponentFactionKey,
    CombatSimulationResult? SingleRound,
    MultiRoundSimulationResult? MultiRound)
{
    private RoundSummary? FirstRound => MultiRound?.RoundSummaries.FirstOrDefault();

    /// <summary>Share of iterations in which the subject won combat resolution.</summary>
    public double WinPct => SingleRound?.AttackerWinPct ?? FirstRound?.AttackerWinPct ?? 0;

    /// <summary>Share of iterations in which combat resolution was level.</summary>
    public double DrawPct => SingleRound?.DrawPct ?? FirstRound?.DrawPct ?? 0;

    /// <summary>Share of iterations in which the opponent won combat resolution.</summary>
    public double LossPct => SingleRound?.DefenderWinPct ?? FirstRound?.DefenderWinPct ?? 0;

    /// <summary>Average combat resolution scored by the subject.</summary>
    public double AvgCombatRes => SingleRound?.AvgAttackerCombatRes ?? FirstRound?.AvgAttackerCombatRes ?? 0;

    /// <summary>Average combat resolution scored by the opponent.</summary>
    public double AvgOpponentCombatRes => SingleRound?.AvgDefenderCombatRes ?? FirstRound?.AvgDefenderCombatRes ?? 0;

    /// <summary>How far ahead on combat resolution the subject is. Negative = losing.</summary>
    public double CombatResMargin => AvgCombatRes - AvgOpponentCombatRes;

    public double AvgWoundsDealt => SingleRound?.AvgAttackerWounds ?? FirstRound?.AvgAttackerWounds ?? 0;

    public double AvgWoundsTaken => SingleRound?.AvgDefenderWounds ?? FirstRound?.AvgDefenderWounds ?? 0;

    /// <summary>
    /// Share of iterations the subject finished the whole engagement — the opponent wiped out
    /// or run down. Only meaningful for multi-round runs, and cap-dependent by nature, so it
    /// is reported alongside the CR verdict rather than in place of it.
    /// </summary>
    public double? DecisiveWinPct => MultiRound?.OutcomePcts
        .Where(kv => kv.Key is CombatOutcome.DefenderWipedOut
            or CombatOutcome.DefenderBrokeCaught
            or CombatOutcome.DefenderBrokeEscaped)
        .Sum(kv => kv.Value);

    /// <summary>Share of iterations the subject was wiped out or run down.</summary>
    public double? DecisiveLossPct => MultiRound?.OutcomePcts
        .Where(kv => kv.Key is CombatOutcome.AttackerWipedOut
            or CombatOutcome.AttackerBrokeCaught
            or CombatOutcome.AttackerBrokeEscaped
            or CombatOutcome.BothWipedOut)
        .Sum(kv => kv.Value);
}
