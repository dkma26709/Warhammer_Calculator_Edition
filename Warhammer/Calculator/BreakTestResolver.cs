using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Calculator;

public enum BreakTestOutcome
{
    Passed,
    Failed,
    Unbreakable,
    UnstableWounds
}

public struct BreakTestResult
{
    public BreakTestOutcome Outcome;
    public int UnstableWoundsDealt;
}

public static class BreakTestResolver
{
    /// <summary>
    /// Resolve a break test for the losing side of a combat round.
    /// </summary>
    /// <param name="crDifference">How much this side lost by (positive = lost).</param>
    /// <param name="leadership">Unit's base Leadership value.</param>
    /// <param name="rules">Unit's effective special rules.</param>
    /// <param name="unitRanks">Complete ranks for this unit (post-casualty).</param>
    /// <param name="opponentRanks">Complete ranks for the opponent.</param>
    /// <param name="rng">Random number generator.</param>
    public static BreakTestResult Resolve(
        int crDifference,
        int leadership,
        ParsedSpecialRules rules,
        int unitRanks,
        int opponentRanks,
        Random rng)
    {
        // Unbreakable (but not Unstable): auto-pass, no roll
        if (rules.Unbreakable && !rules.Unstable)
            return new BreakTestResult { Outcome = BreakTestOutcome.Unbreakable };

        // Unstable: doesn't take a break test, instead takes wounds equal to CR difference
        if (rules.Unstable)
        {
            bool steadfast = rules.Stubborn || unitRanks > opponentRanks;
            int wounds = crDifference;

            // Unstable: "If an Unstable unit would normally be eligible to be Steadfast, they
            // instead suffer 1 less Wound than they would normally do."
            if (steadfast)
                wounds -= 1;

            // Indomitable stacks on top of that: it reduces the wounds the unit "normally would"
            // suffer, and the steadfast reduction is part of what it normally would suffer.
            if (rules.Indomitable)
                wounds -= 1;

            return new BreakTestResult
            {
                Outcome = BreakTestOutcome.UnstableWounds,
                UnstableWoundsDealt = Math.Max(0, wounds)
            };
        }

        // Determine effective Ld target
        bool isSteadfast = rules.Stubborn || unitRanks > opponentRanks;
        int target = isSteadfast ? leadership : leadership - crDifference;

        // Roll dice
        int roll;
        if (rules.ColdBlooded)
        {
            // Roll 3d6, drop highest
            int d1 = rng.Next(1, 7);
            int d2 = rng.Next(1, 7);
            int d3 = rng.Next(1, 7);
            roll = d1 + d2 + d3 - Math.Max(d1, Math.Max(d2, d3));
        }
        else
        {
            roll = rng.Next(1, 7) + rng.Next(1, 7);
        }

        return new BreakTestResult
        {
            Outcome = roll <= target ? BreakTestOutcome.Passed : BreakTestOutcome.Failed
        };
    }
}
