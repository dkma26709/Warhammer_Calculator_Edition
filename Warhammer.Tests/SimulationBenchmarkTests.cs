using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Services;
using Xunit.Abstractions;

namespace Warhammer.Tests;

public class SimulationBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public SimulationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Convergence_10K_WithinThreePercentOfBaseline()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 3, t: 3, a: 1, specialRules: "Fight in Extra Ranks (1)"))
            { ModelCount = 20, Frontage = 5, SelectedWeaponKey = "spear" };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, a: 1))
            { ModelCount = 20, Frontage = 5, SelectedWeaponKey = "hand_weapon" };
        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

        // Baseline: 100K iterations
        var baseline = engine.Simulate(attacker, defender, context, 100_000);

        // 10K should converge within 3 percentage points
        var tenK = engine.Simulate(attacker, defender, context, 10_000);
        double maxDrift = new[]
        {
            Math.Abs(tenK.AttackerWinPct - baseline.AttackerWinPct),
            Math.Abs(tenK.DefenderWinPct - baseline.DefenderWinPct),
            Math.Abs(tenK.DrawPct - baseline.DrawPct),
        }.Max();

        _output.WriteLine($"Baseline (100K): Win={baseline.AttackerWinPct}% Loss={baseline.DefenderWinPct}% Draw={baseline.DrawPct}%");
        _output.WriteLine($"Test (10K):      Win={tenK.AttackerWinPct}% Loss={tenK.DefenderWinPct}% Draw={tenK.DrawPct}%");
        _output.WriteLine($"Max drift: {maxDrift:F2}pp");

        Assert.True(maxDrift < 3.0, $"10K drift {maxDrift:F2}pp exceeds 3pp threshold");
    }

    [Fact]
    public void ComplexMatchup_ProducesReasonableResults()
    {
        // Complex matchup with many interacting rules — verify nothing blows up
        // and the favoured side actually wins more often.
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 5, s: 4, t: 4, w: 1, a: 2,
            specialRules: "Killing Blow, Hatred, Frenzy, Armour Piercing (1)"))
            { ModelCount = 20, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(
            ws: 4, s: 4, t: 4, w: 1, a: 1,
            specialRules: "Regeneration (5+), Ward Save (6+), Natural Armour (6+)"))
            { ModelCount = 20, Frontage = 5 };
        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

        var result = engine.Simulate(attacker, defender, context, 10_000);

        _output.WriteLine($"Win={result.AttackerWinPct}% Loss={result.DefenderWinPct}% Draw={result.DrawPct}%");
        _output.WriteLine($"Avg wounds: attacker={result.AvgAttackerWounds}, defender={result.AvgDefenderWounds}");

        // Attacker has higher WS, more attacks, Hatred+Frenzy+KB+AP — should dominate
        Assert.True(result.AttackerWinPct > result.DefenderWinPct,
            $"KB+Hatred+Frenzy attacker should win more: {result.AttackerWinPct}% vs {result.DefenderWinPct}%");
        Assert.True(result.AttackerWinPct > 50,
            $"Attacker should win majority: {result.AttackerWinPct}%");
    }
}
