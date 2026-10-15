using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Tests;

/// <summary>
/// The overall prevail split the Matchup page shows: every multi-round outcome counts towards
/// exactly one of attacker prevails, defender prevails, or neither.
/// </summary>
public class MultiRoundPrevailTests
{
    [Fact]
    public void PrevailSplit_SumsTheOutcomesOnEachSide()
    {
        var result = new MultiRoundSimulationResult
        {
            OutcomePcts = new Dictionary<CombatOutcome, double>
            {
                [CombatOutcome.DefenderWipedOut] = 30,
                [CombatOutcome.DefenderBrokeCaught] = 10,
                [CombatOutcome.DefenderBrokeEscaped] = 5,
                [CombatOutcome.AttackerWipedOut] = 20,
                [CombatOutcome.AttackerBrokeCaught] = 15,
                [CombatOutcome.AttackerBrokeEscaped] = 5,
                [CombatOutcome.BothWipedOut] = 5,
                [CombatOutcome.MaxRoundsReached] = 10,
            }
        };

        Assert.Equal(45, result.AttackerPrevailsPct);
        Assert.Equal(40, result.DefenderPrevailsPct);
        Assert.Equal(15, result.NeitherPrevailsPct);
    }

    [Fact]
    public void PrevailSplit_OfNoOutcomes_IsZero()
    {
        var result = new MultiRoundSimulationResult();

        Assert.Equal(0, result.AttackerPrevailsPct);
        Assert.Equal(0, result.DefenderPrevailsPct);
        Assert.Equal(0, result.NeitherPrevailsPct);
    }

    [Fact]
    public void PrevailSplit_OfASimulatedCombat_AddsUpToTheWhole()
    {
        var engine = new CombatEngine(TestHelpers.Repo);

        // An even fight capped at three rounds, so all three shares turn up.
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, i: 3, a: 1, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, i: 3, a: 1, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };

        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 3, iterations: 2_000, seed: 11);

        Assert.True(result.AttackerPrevailsPct > 0, "the attacker should prevail in some combats");
        Assert.True(result.DefenderPrevailsPct > 0, "the defender should prevail in some combats");
        Assert.True(result.NeitherPrevailsPct > 0, "some combats should reach the round cap");
        // Each outcome share is rounded to 0.1, so the total may miss 100 by a few tenths.
        Assert.Equal(100, result.AttackerPrevailsPct + result.DefenderPrevailsPct + result.NeitherPrevailsPct, 0.5);
    }
}
