using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Shared test infrastructure. Loads core rules once, provides unit factories and statistical helpers.
/// </summary>
public static class TestHelpers
{
    public static readonly CoreRulesData CoreData;

    static TestHelpers()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data");
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
    }

    public static string FindProjectRoot()
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

    public static CoreRulesService CreateCoreRules()
    {
        var service = new CoreRulesService(null!);
        var dataField = typeof(CoreRulesService).GetField("_data",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(CoreRulesService).GetField("_loaded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        dataField.SetValue(service, CoreData);
        loadedField.SetValue(service, true);
        return service;
    }

    /// <summary>
    /// Create a GameDataRepository with core rules pre-loaded (weapons + armour resolved).
    /// Uses synchronous initialization to avoid async in tests.
    /// </summary>
    public static GameDataRepository CreateRepository()
    {
        var coreRules = CreateCoreRules();
        var repo = new GameDataRepository(coreRules, null!);
        // Force EnsureLoadedAsync synchronously — core rules already loaded via reflection
        repo.EnsureLoadedAsync().GetAwaiter().GetResult();
        return repo;
    }

    public static UnitEntry MakeUnit(
        string name = "Test Unit",
        string troopType = "Infantry",
        int m = 4, int ws = 4, int bs = 3, int s = 3, int t = 3, int w = 1, int i = 3, int a = 1, int ld = 7,
        double? points = 8,
        string? equipment = "Hand weapon",
        string? specialRules = null,
        string? unitSize = "10+",
        string? baseSize = "25x25",
        ProfileEntry? mountProfile = null,
        List<UnitOption>? options = null)
    {
        var profiles = new List<ProfileEntry>
        {
            new() { Name = name, M = m, WS = ws, BS = bs, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points }
        };

        if (mountProfile != null)
            profiles.Add(mountProfile);

        return new UnitEntry
        {
            Name = name,
            TroopType = troopType,
            Profiles = profiles,
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize,
            BaseSize = baseSize,
            Options = options
        };
    }

    // ─── Statistical Helpers ────────────────────────────────────────

    /// <summary>
    /// Runs a combat scenario many times and aggregates AttackResult totals.
    /// </summary>
    public static AggregatedAttackResult RunAttackTrials(
        int iterations,
        Func<Random, AttackResult> trial)
    {
        long totalHits = 0, totalWounds = 0, totalKBWounds = 0, totalUnsaved = 0;

        for (int seed = 0; seed < iterations; seed++)
        {
            var rng = new Random(seed);
            var result = trial(rng);
            totalHits += result.TotalHits;
            totalWounds += result.TotalWoundsInflicted;
            totalKBWounds += result.KillingBlowWounds;
            totalUnsaved += result.UnsavedWounds;
        }

        return new AggregatedAttackResult(iterations, totalHits, totalWounds, totalKBWounds, totalUnsaved);
    }

    /// <summary>
    /// Runs a combat round scenario many times and aggregates RoundResult totals.
    /// </summary>
    public static AggregatedRoundResult RunRoundTrials(
        int iterations,
        Func<Random, RoundResult> trial)
    {
        long totalAttWounds = 0, totalDefWounds = 0;

        for (int seed = 0; seed < iterations; seed++)
        {
            var rng = new Random(seed);
            var result = trial(rng);
            totalAttWounds += result.AttackerWoundsDealt;
            totalDefWounds += result.DefenderWoundsDealt;
        }

        return new AggregatedRoundResult(iterations, totalAttWounds, totalDefWounds);
    }

    /// <summary>
    /// Asserts that an observed rate falls within an expected range.
    /// </summary>
    public static void AssertRate(double observed, double expectedMin, double expectedMax, string label)
    {
        Assert.True(observed >= expectedMin && observed <= expectedMax,
            $"{label}: expected [{expectedMin:F3}–{expectedMax:F3}], got {observed:F3}");
    }

    /// <summary>
    /// Asserts that A is meaningfully greater than B (by at least minRatio).
    /// </summary>
    public static void AssertGreaterByRatio(long a, long b, double minRatio, string label)
    {
        double ratio = b > 0 ? (double)a / b : double.MaxValue;
        Assert.True(ratio >= minRatio,
            $"{label}: expected ratio >= {minRatio:F2}, got {ratio:F3} (A={a}, B={b})");
    }
}

public record AggregatedAttackResult(int Trials, long TotalHits, long TotalWounds, long TotalKBWounds, long TotalUnsaved)
{
    public double AvgHits => (double)TotalHits / Trials;
    public double AvgWounds => (double)TotalWounds / Trials;
    public double AvgKBWounds => (double)TotalKBWounds / Trials;
    public double AvgUnsaved => (double)TotalUnsaved / Trials;
    public double HitRate(int attacksPerTrial) => (double)TotalHits / (Trials * attacksPerTrial);
    public double WoundRate(int attacksPerTrial) => (double)TotalWounds / (Trials * attacksPerTrial);
}

public record AggregatedRoundResult(int Trials, long TotalAttackerWounds, long TotalDefenderWounds)
{
    public double AvgAttackerWounds => (double)TotalAttackerWounds / Trials;
    public double AvgDefenderWounds => (double)TotalDefenderWounds / Trials;
}
