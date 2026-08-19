using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class MultiRoundCombatTests
{
    [Fact]
    public void MultiRound_WeakVsStrong_StrongSideWins()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        // Strong attacker (S5, A2, high WS) vs weak defender (S3, A1, low WS)
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 5, s: 5, i: 4, a: 2, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 2, s: 3, t: 3, i: 3, a: 1, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 6, iterations: 2_000);

        // The strong side should win most of the time (wiped out, broke & caught, or broke & escaped)
        double defWipedPct = result.OutcomePcts.GetValueOrDefault(CombatOutcome.DefenderWipedOut);
        double defCaughtPct = result.OutcomePcts.GetValueOrDefault(CombatOutcome.DefenderBrokeCaught);
        double defEscapedPct = result.OutcomePcts.GetValueOrDefault(CombatOutcome.DefenderBrokeEscaped);
        double totalWin = defWipedPct + defCaughtPct + defEscapedPct;
        Assert.True(totalWin > 80,
            $"Strong attacker should win >80% (wiped={defWipedPct}%, caught={defCaughtPct}%, escaped={defEscapedPct}%)");
    }

    [Fact]
    public void MultiRound_MaxRoundsCap_Respected()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        // Two Unbreakable units that can barely wound each other — never break
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 3, s: 2, t: 5, i: 3, a: 1, specialRules: "Unbreakable", unitSize: "20+"))
            { ModelCount = 20, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 3, s: 2, t: 5, i: 3, a: 1, specialRules: "Unbreakable", unitSize: "20+"))
            { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 3, iterations: 1_000);

        // Unbreakable + S2 vs T5 → almost all iterations should hit the 3-round cap
        double maxRoundsPct = result.OutcomePcts.GetValueOrDefault(CombatOutcome.MaxRoundsReached);
        Assert.True(maxRoundsPct > 90,
            $"Unbreakable tough units should hit max rounds >90% of the time, got {maxRoundsPct}%");
        Assert.Equal(3, result.MaxRounds);
    }

    [Fact]
    public void MultiRound_CasualtiesCarryBetweenRounds()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        // Stubborn units so they don't break — 10 vs 10, moderate damage
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 4, i: 3, a: 1, specialRules: "Stubborn", ld: 9, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 4, t: 3, i: 3, a: 1, specialRules: "Stubborn", ld: 9, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 4, iterations: 2_000);

        // Should have at least 2 rounds of data
        Assert.True(result.RoundSummaries.Count >= 2,
            "Should have at least 2 rounds of data");

        var round1 = result.RoundSummaries[0];
        var round2 = result.RoundSummaries[1];

        Assert.True(round1.AvgAttackerModelsRemaining > round2.AvgAttackerModelsRemaining,
            $"Attacker models should decline: R1={round1.AvgAttackerModelsRemaining}, R2={round2.AvgAttackerModelsRemaining}");
    }

    [Fact]
    public void MultiRound_Round2HasNoChargeBonus()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        // Stubborn so they don't break after round 1
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 3, i: 3, a: 1, specialRules: "Stubborn", ld: 9, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 3, t: 3, i: 3, a: 1, specialRules: "Stubborn", ld: 9, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext
        {
            Charge = ChargeState.AttackerCharged,
            CombatRound = 1,
        };

        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 3, iterations: 2_000);

        // Round 1 should show charge bonus
        var r1 = result.RoundSummaries[0];
        Assert.True(r1.AvgAttackerBreakdown.ChargeBonus > 0.9,
            $"Round 1 should have charge bonus, got {r1.AvgAttackerBreakdown.ChargeBonus}");

        // Round 2 should have no charge bonus
        if (result.RoundSummaries.Count >= 2)
        {
            var r2 = result.RoundSummaries[1];
            Assert.Equal(0, r2.AvgAttackerBreakdown.ChargeBonus);
        }
    }

    [Fact]
    public void MultiRound_PerRoundStats_HaveFullBreakdown()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 3, i: 3, a: 1, specialRules: "Unbreakable", unitSize: "20+"))
            { ModelCount = 20, Frontage = 5, HasStandardBearer = true };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 3, t: 3, i: 3, a: 1, specialRules: "Unbreakable", unitSize: "20+"))
            { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 3, iterations: 2_000);

        // Every round should have full CR breakdown data
        foreach (var round in result.RoundSummaries)
        {
            Assert.True(round.AvgAttackerWounds > 0, $"Round {round.Round}: attacker should deal wounds");
            Assert.True(round.AvgAttackerBreakdown.Wounds > 0, $"Round {round.Round}: wounds breakdown should be populated");
            Assert.True(round.AvgAttackerBreakdown.RankBonus > 0, $"Round {round.Round}: rank bonus should be populated");
            Assert.True(round.AvgAttackerBreakdown.StandardBearer > 0, $"Round {round.Round}: standard should be populated");
            Assert.True(round.IterationsReached > 0, $"Round {round.Round}: should have iterations");
        }
    }

    [Fact]
    public void MultiRound_Round1_MatchesSingleRound()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, i: 3, a: 1, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };

        var singleResult = engine.Simulate(attacker, defender, context, iterations: 5_000);
        var multiResult = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 6, iterations: 5_000);

        // Round 1 stats should be in the same ballpark (different RNG seeds, but same distribution)
        double singleAvg = singleResult.AvgAttackerWounds;
        double multiAvg = multiResult.RoundSummaries[0].AvgAttackerWounds;

        Assert.True(Math.Abs(singleAvg - multiAvg) < 0.5,
            $"Round 1 stats should be similar: single={singleAvg}, multi={multiAvg}");
    }

}

/// <summary>
/// Wall-clock assertions run alone: xUnit runs test classes in parallel, so on a shared CI
/// runner the other simulation tests steal the cores this stopwatch is timing. A
/// parallel-disabled collection runs after the parallel phase, with the process to itself.
/// </summary>
[CollectionDefinition(nameof(PerformanceGated), DisableParallelization = true)]
public class PerformanceGated;

[Collection(nameof(PerformanceGated))]
public class MultiRoundPerformanceTests
{
    [Fact]
    public void MultiRound_PerformanceAcceptable()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1, unitSize: "20+"))
            { ModelCount = 20, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, i: 3, a: 1, unitSize: "20+"))
            { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { CombatRound = 1 };
        var result = engine.SimulateMultiRound(attacker, defender, context, maxRounds: 6, iterations: 10_000);

        // ~680ms on a dev machine; the budget allows a hosted runner running it solo to be
        // 4x slower while still catching a real order-of-magnitude regression.
        Assert.True(result.ElapsedMs < 3_000,
            $"10K iterations × 6 rounds took {result.ElapsedMs}ms, expected < 3000ms");
    }
}
