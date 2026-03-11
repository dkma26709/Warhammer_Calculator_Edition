using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;
using Xunit.Abstractions;

namespace Warhammer.Tests;

public class SimulationBenchmarkTests
{
    private static readonly CoreRulesData CoreData;
    private readonly ITestOutputHelper _output;

    static SimulationBenchmarkTests()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data");
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
    }

    public SimulationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Warhammer.csproj")))
                return dir;
            var sibling = Path.Combine(Path.GetDirectoryName(dir)!, "Warhammer");
            if (Directory.Exists(sibling) && File.Exists(Path.Combine(sibling, "Warhammer.csproj")))
                return sibling;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Warhammer project root");
    }

    private static CoreRulesService CreateCoreRules()
    {
        var service = new CoreRulesService(null!);
        var dataField = typeof(CoreRulesService).GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(CoreRulesService).GetField("_loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        dataField.SetValue(service, CoreData);
        loadedField.SetValue(service, true);
        return service;
    }

    private static UnitEntry MakeUnit(
        string troopType = "Infantry",
        int ws = 4, int s = 3, int t = 3, int w = 1, int i = 3, int a = 1, int ld = 7,
        double? points = 8,
        string? equipment = "Hand weapon",
        string? specialRules = null,
        string? unitSize = "10+")
    {
        var profiles = new List<ProfileEntry>
        {
            new() { Name = "Test", WS = ws, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points }
        };

        return new UnitEntry
        {
            Name = "Test Unit",
            TroopType = troopType,
            Profiles = profiles,
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize
        };
    }

    /// <summary>
    /// Runs simulations at various iteration counts and reports elapsed time
    /// plus convergence relative to a high-iteration baseline.
    /// </summary>
    [Fact]
    public void Benchmark_IterationCountVsTimeAndConvergence()
    {
        var coreRules = CreateCoreRules();
        var engine = new CombatEngine(coreRules);

        var attacker = new UnitConfiguration(MakeUnit(ws: 4, s: 3, t: 3, a: 1, specialRules: "Fight in Extra Ranks (1)"))
            { ModelCount = 20, Frontage = 5, SelectedWeaponKey = "spear" };
        var defender = new UnitConfiguration(MakeUnit(ws: 4, s: 3, t: 3, a: 1))
            { ModelCount = 20, Frontage = 5, SelectedWeaponKey = "hand_weapon" };

        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

        int[] iterationCounts = [500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000];
        const int runs = 3;

        // Warmup JIT
        engine.Simulate(attacker, defender, context, 1_000);

        // Baseline: average of 3 runs at 100K
        var baselineResults = Enumerable.Range(0, runs)
            .Select(_ => engine.Simulate(attacker, defender, context, 100_000))
            .ToList();
        double baselineWin = baselineResults.Average(r => r.AttackerWinPct);
        double baselineLoss = baselineResults.Average(r => r.DefenderWinPct);
        double baselineDraw = baselineResults.Average(r => r.DrawPct);

        _output.WriteLine("");
        _output.WriteLine("Iters      | Time (ms) | Win%   / Loss%  / Draw%   | Max Drift");
        _output.WriteLine("-----------|-----------|---------------------------|----------");

        foreach (var count in iterationCounts)
        {
            var results = Enumerable.Range(0, runs)
                .Select(_ => engine.Simulate(attacker, defender, context, count))
                .ToList();

            double avgMs = results.Average(r => r.ElapsedMs);
            double avgWin = results.Average(r => r.AttackerWinPct);
            double avgLoss = results.Average(r => r.DefenderWinPct);
            double avgDraw = results.Average(r => r.DrawPct);

            double driftWin = Math.Abs(avgWin - baselineWin);
            double driftLoss = Math.Abs(avgLoss - baselineLoss);
            double driftDraw = Math.Abs(avgDraw - baselineDraw);
            double maxDrift = Math.Max(driftWin, Math.Max(driftLoss, driftDraw));

            _output.WriteLine(
                $"{count,10:N0} | {avgMs,9:N1} | {avgWin,5:F1}% / {avgLoss,5:F1}% / {avgDraw,5:F1}% | {maxDrift,6:F2}pp");
        }

        _output.WriteLine("");
        _output.WriteLine($"Baseline (100K avg): Win={baselineWin:F1}% Loss={baselineLoss:F1}% Draw={baselineDraw:F1}%");

        // Sanity: 10K should drift < 3pp from 100K baseline
        var tenK = engine.Simulate(attacker, defender, context, 10_000);
        double tenKMaxDrift = new[]
        {
            Math.Abs(tenK.AttackerWinPct - baselineWin),
            Math.Abs(tenK.DefenderWinPct - baselineLoss),
            Math.Abs(tenK.DrawPct - baselineDraw)
        }.Max();
        Assert.True(tenKMaxDrift < 3.0, $"10K drift {tenKMaxDrift:F2}pp exceeds 3pp threshold");
    }

    /// <summary>
    /// Tests a more complex matchup with many special rules to measure worst-case timing.
    /// </summary>
    [Fact]
    public void Benchmark_ComplexMatchup_Timing()
    {
        var coreRules = CreateCoreRules();
        var engine = new CombatEngine(coreRules);

        var attacker = new UnitConfiguration(MakeUnit(
            ws: 5, s: 4, t: 4, w: 1, a: 2,
            specialRules: "Killing Blow, Hatred, Frenzy, Armour Piercing (1)"))
            { ModelCount = 20, Frontage = 5 };
        var defender = new UnitConfiguration(MakeUnit(
            ws: 4, s: 4, t: 4, w: 1, a: 1,
            specialRules: "Regeneration (5+), Ward Save (6+), Natural Armour (6+)"))
            { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };

        // Warmup
        engine.Simulate(attacker, defender, context, 1_000);

        int[] iterationCounts = [1_000, 5_000, 10_000, 25_000, 50_000];
        const int runs = 3;

        _output.WriteLine("");
        _output.WriteLine("Complex matchup (KB + Hatred + Frenzy + AP vs Regen + Ward + Armour):");
        _output.WriteLine("");
        _output.WriteLine("Iters      | Time (ms) | Win%   / Loss%  / Draw%");
        _output.WriteLine("-----------|-----------|------------------------");

        foreach (var count in iterationCounts)
        {
            var results = Enumerable.Range(0, runs)
                .Select(_ => engine.Simulate(attacker, defender, context, count))
                .ToList();

            double avgMs = results.Average(r => r.ElapsedMs);
            double avgWin = results.Average(r => r.AttackerWinPct);
            double avgLoss = results.Average(r => r.DefenderWinPct);
            double avgDraw = results.Average(r => r.DrawPct);

            _output.WriteLine(
                $"{count,10:N0} | {avgMs,9:N1} | {avgWin,5:F1}% / {avgLoss,5:F1}% / {avgDraw,5:F1}%");
        }
    }
}
