using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class BreakTestResolverTests
{
    private const int Iterations = 5_000;

    private static double PassRate(
        int crDifference, int leadership, ParsedSpecialRules rules,
        int unitRanks = 1, int opponentRanks = 1)
    {
        int passed = 0;
        for (int seed = 0; seed < Iterations; seed++)
        {
            var result = BreakTestResolver.Resolve(
                crDifference, leadership, rules, unitRanks, opponentRanks, new Random(seed));
            if (result.Outcome == BreakTestOutcome.Passed)
                passed++;
        }
        return (double)passed / Iterations;
    }

    // ─── Normal Break Tests ──────────────────────────────────────────

    [Fact]
    public void Normal_Ld7_LostBy2_PassRate_Around28Percent()
    {
        // Target = 7 - 2 = 5 on 2d6. P(2d6 ≤ 5) = 10/36 ≈ 27.8%
        double rate = PassRate(2, 7, new ParsedSpecialRules());
        TestHelpers.AssertRate(rate, 0.23, 0.33, "Ld7 lost-by-2 pass rate");
    }

    [Fact]
    public void Normal_Ld8_LostBy1_PassRate_Around58Percent()
    {
        // Target = 8 - 1 = 7 on 2d6. P(2d6 ≤ 7) = 21/36 ≈ 58.3%
        double rate = PassRate(1, 8, new ParsedSpecialRules());
        TestHelpers.AssertRate(rate, 0.53, 0.63, "Ld8 lost-by-1 pass rate");
    }

    [Fact]
    public void Normal_Ld5_LostBy5_AlmostAlwaysFails()
    {
        // Target = 5 - 5 = 0 on 2d6. Impossible to roll ≤ 0
        double rate = PassRate(5, 5, new ParsedSpecialRules());
        Assert.Equal(0, rate);
    }

    // ─── Stubborn ────────────────────────────────────────────────────

    [Fact]
    public void Stubborn_IgnoresCRDifference()
    {
        // Stubborn Ld 9: always tests on 9 regardless of CR difference
        // P(2d6 ≤ 9) = 30/36 ≈ 83.3%
        var rules = new ParsedSpecialRules { Stubborn = true };

        double lostBy1 = PassRate(1, 9, rules);
        double lostBy5 = PassRate(5, 9, rules);

        TestHelpers.AssertRate(lostBy1, 0.78, 0.88, "Stubborn Ld9 lost-by-1");
        TestHelpers.AssertRate(lostBy5, 0.78, 0.88, "Stubborn Ld9 lost-by-5");

        // Both should be roughly equal (Stubborn ignores difference)
        Assert.True(Math.Abs(lostBy1 - lostBy5) < 0.05,
            $"Stubborn rates should be equal: {lostBy1:F3} vs {lostBy5:F3}");
    }

    // ─── Steadfast ───────────────────────────────────────────────────

    [Fact]
    public void Steadfast_MoreRanks_UsesUnmodifiedLd()
    {
        // Unit has 3 ranks vs opponent's 1 rank → Steadfast → unmodified Ld
        var rules = new ParsedSpecialRules();

        double steadfastRate = PassRate(3, 7, rules, unitRanks: 3, opponentRanks: 1);
        double normalRate = PassRate(3, 7, rules, unitRanks: 1, opponentRanks: 3);

        // Steadfast: target = 7. P(2d6 ≤ 7) = 58.3%
        TestHelpers.AssertRate(steadfastRate, 0.53, 0.63, "Steadfast rate");
        // Normal: target = 7 - 3 = 4. P(2d6 ≤ 4) = 6/36 ≈ 16.7%
        TestHelpers.AssertRate(normalRate, 0.12, 0.22, "Non-steadfast rate");
    }

    // ─── Cold-Blooded ────────────────────────────────────────────────

    [Fact]
    public void ColdBlooded_PassesMoreOftenThanNormal()
    {
        var normal = new ParsedSpecialRules();
        var coldBlooded = new ParsedSpecialRules { ColdBlooded = true };

        double normalRate = PassRate(2, 7, normal);
        double cbRate = PassRate(2, 7, coldBlooded);

        Assert.True(cbRate > normalRate,
            $"Cold-Blooded ({cbRate:F3}) should pass more than normal ({normalRate:F3})");
    }

    // ─── Unbreakable ─────────────────────────────────────────────────

    [Fact]
    public void Unbreakable_NeverBreaks()
    {
        var rules = new ParsedSpecialRules { Unbreakable = true };

        for (int seed = 0; seed < 100; seed++)
        {
            var result = BreakTestResolver.Resolve(10, 1, rules, 1, 5, new Random(seed));
            Assert.Equal(BreakTestOutcome.Unbreakable, result.Outcome);
        }
    }

    // ─── Unstable ────────────────────────────────────────────────────

    [Fact]
    public void Unstable_TakesWoundsEqualToCRDifference()
    {
        var rules = new ParsedSpecialRules { Unstable = true, Unbreakable = true };

        var result = BreakTestResolver.Resolve(3, 7, rules, 1, 1, new Random(42));

        Assert.Equal(BreakTestOutcome.UnstableWounds, result.Outcome);
        Assert.Equal(3, result.UnstableWoundsDealt);
    }

    [Fact]
    public void Unstable_Steadfast_ReducesWoundsByOne()
    {
        var rules = new ParsedSpecialRules { Unstable = true, Unbreakable = true };

        // Unit has 3 ranks vs opponent's 1 → Steadfast → 3 - 1 = 2 wounds
        var result = BreakTestResolver.Resolve(3, 7, rules, unitRanks: 3, opponentRanks: 1, new Random(42));

        Assert.Equal(BreakTestOutcome.UnstableWounds, result.Outcome);
        Assert.Equal(2, result.UnstableWoundsDealt);
    }

    [Fact]
    public void Unstable_Stubborn_AlsoReducesByOne()
    {
        var rules = new ParsedSpecialRules { Unstable = true, Unbreakable = true, Stubborn = true };

        var result = BreakTestResolver.Resolve(5, 7, rules, unitRanks: 1, opponentRanks: 3, new Random(42));

        Assert.Equal(BreakTestOutcome.UnstableWounds, result.Outcome);
        Assert.Equal(4, result.UnstableWoundsDealt); // 5 - 1
    }

    [Fact]
    public void Unstable_LostByZero_NoWounds()
    {
        var rules = new ParsedSpecialRules { Unstable = true, Unbreakable = true };

        var result = BreakTestResolver.Resolve(0, 7, rules, 1, 1, new Random(42));

        Assert.Equal(BreakTestOutcome.UnstableWounds, result.Outcome);
        Assert.Equal(0, result.UnstableWoundsDealt);
    }
}
