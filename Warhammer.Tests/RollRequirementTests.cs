using Warhammer.Models.Domain;

namespace Warhammer.Tests;

/// <summary>
/// The combat-chart ladder: 2+ 3+ 4+ 5+ 6+ 6+/2+ 6+/3+ 6+/4+ 6+/5+ 6+/6+ n/a. Both to-hit
/// charts are entirely plain scores; only wap-v3's to-wound chart reaches the compound rungs.
/// </summary>
public class RollRequirementTests
{
    // ─── Parsing chart cells ────────────────────────────────────────

    [Theory]
    [InlineData("2+", 2, null)]
    [InlineData("4+", 4, null)]
    [InlineData("6+", 6, null)]
    [InlineData("6+/2+", 6, 2)]
    [InlineData("6+/6+", 6, 6)]
    [InlineData("4", 4, null)]          // legacy bare int from wap-v2 data
    [InlineData(" 6+ / 3+ ", 6, 3)]
    public void Parse_ReadsChartCells(string cell, int expectedFirst, int? expectedSecond)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        Assert.Equal(expectedFirst, req.FirstRoll);
        Assert.Equal(expectedSecond, req.SecondRoll);
        Assert.True(req.CanSucceed);
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("N/A")]
    [InlineData("na")]
    [InlineData("-")]
    public void Parse_ReadsImpossible(string cell)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        Assert.False(req.CanSucceed);
        Assert.Equal(0.0, req.Probability);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("banana")]
    [InlineData("7+")]
    [InlineData("5+/2+")]     // only a 6 may carry a confirming roll
    [InlineData("6+/2+/3+")]
    public void Parse_RejectsMalformedCells(string? cell)
    {
        Assert.False(RollRequirement.TryParse(cell, out _));
    }

    [Theory]
    [InlineData("4+")]
    [InlineData("6+/3+")]
    [InlineData("n/a")]
    public void ToString_RoundTrips(string cell)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        Assert.Equal(cell, req.ToString());
    }

    // ─── The book's worked example ──────────────────────────────────

    [Fact]
    public void ModifierSpendsTheSecondRollFirst()
    {
        // "a Strength 2 Attack with a +1 modifier treats Toughness 5 as only requiring to
        // roll a 6+ To Wound, rather than a 6+ followed by a 2+."
        var s2VsT5 = RollRequirement.Compound(2);          // 6+/2+
        var improved = s2VsT5.Step(-1);                        // +1 to the roll = one step easier

        Assert.Equal(6, improved.FirstRoll);
        Assert.Null(improved.SecondRoll);
        Assert.Equal("6+", improved.ToString());
    }

    // ─── Ladder arithmetic ─────────────────────────────────────────

    [Theory]
    [InlineData("6+/6+", -1, "6+/5+")]
    [InlineData("6+/3+", -1, "6+/2+")]
    [InlineData("6+/2+", -1, "6+")]
    [InlineData("6+", -1, "5+")]
    [InlineData("3+", -1, "2+")]
    [InlineData("2+", -1, "2+")]        // cannot improve past 2+
    [InlineData("2+", -5, "2+")]
    [InlineData("4+", -2, "2+")]
    [InlineData("6+/2+", -3, "4+")]
    public void Step_Easier_WalksDownTheLadder(string from, int steps, string expected)
    {
        Assert.True(RollRequirement.TryParse(from, out var req));
        Assert.Equal(expected, req.Step(steps).ToString());
    }

    [Theory]
    [InlineData("2+", 1, "3+")]
    [InlineData("5+", 1, "6+")]
    [InlineData("6+", 1, "6+/2+")]
    [InlineData("6+/2+", 1, "6+/3+")]
    [InlineData("6+/6+", 1, "n/a")]
    [InlineData("n/a", 1, "n/a")]
    [InlineData("n/a", -1, "6+/6+")]
    [InlineData("6+", 6, "n/a")]
    public void Step_Harder_WalksUpTheLadder(string from, int steps, string expected)
    {
        Assert.True(RollRequirement.TryParse(from, out var req));
        Assert.Equal(expected, req.Step(steps).ToString());
    }

    [Fact]
    public void Step_IsReversible_AwayFromTheEnds()
    {
        foreach (var cell in new[] { "2+", "4+", "6+", "6+/2+", "6+/4+" })
        {
            Assert.True(RollRequirement.TryParse(cell, out var req));
            Assert.Equal(cell, req.Step(2).Step(-2).ToString());
        }
    }

    [Fact]
    public void Step_SaturatesAtBothEnds()
    {
        // 2+ and n/a are absorbing, so stepping past either loses where you came from.
        // Deliberate: the chart has no rung better than 2+ or worse than "cannot wound".
        Assert.True(RollRequirement.TryParse("6+/6+", out var hardest));
        Assert.Equal("n/a", hardest.Step(2).ToString());
        Assert.Equal("6+/5+", hardest.Step(2).Step(-2).ToString());

        Assert.True(RollRequirement.TryParse("2+", out var easiest));
        Assert.Equal("2+", easiest.Step(-3).ToString());
        Assert.Equal("5+", easiest.Step(-3).Step(3).ToString());
    }

    [Fact]
    public void Step_ZeroIsIdentity()
    {
        Assert.True(RollRequirement.TryParse("6+/4+", out var req));
        Assert.Equal(req, req.Step(0));
    }

    // ─── Probability ───────────────────────────────────────────────

    [Theory]
    [InlineData("2+", 5.0 / 6.0)]
    [InlineData("4+", 3.0 / 6.0)]
    [InlineData("6+", 1.0 / 6.0)]
    public void Probability_SingleRoll(string cell, double expected)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        Assert.Equal(expected, req.Probability, 10);
    }

    [Theory]
    [InlineData("6+/2+", (1.0 / 6.0) * (5.0 / 6.0))]
    [InlineData("6+/4+", (1.0 / 6.0) * (3.0 / 6.0))]
    [InlineData("6+/6+", (1.0 / 6.0) * (1.0 / 6.0))]
    public void Probability_CompoundMultipliesBothRolls(string cell, double expected)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        Assert.Equal(expected, req.Probability, 10);
    }

    [Fact]
    public void Probability_DecreasesMonotonicallyUpTheLadder()
    {
        var ladder = new[] { "2+", "3+", "4+", "5+", "6+", "6+/2+", "6+/3+", "6+/4+", "6+/5+", "6+/6+", "n/a" };
        double previous = 1.0;
        foreach (var cell in ladder)
        {
            Assert.True(RollRequirement.TryParse(cell, out var req));
            Assert.True(req.Probability < previous,
                $"{cell} ({req.Probability:F4}) should be strictly harder than the rung before it ({previous:F4})");
            previous = req.Probability;
        }
    }

    // ─── Rolling ───────────────────────────────────────────────────

    [Theory]
    [InlineData("2+")]
    [InlineData("4+")]
    [InlineData("6+")]
    [InlineData("6+/2+")]
    [InlineData("6+/4+")]
    [InlineData("6+/6+")]
    public void Roll_MatchesItsStatedProbability(string cell)
    {
        Assert.True(RollRequirement.TryParse(cell, out var req));
        var rng = new Random(20260805);

        const int trials = 200_000;
        int successes = 0;
        for (int i = 0; i < trials; i++)
            if (req.Roll(rng).Success) successes++;

        double observed = (double)successes / trials;
        Assert.InRange(observed, req.Probability - 0.005, req.Probability + 0.005);
    }

    [Fact]
    public void Roll_Impossible_NeverSucceeds()
    {
        var rng = new Random(1);
        for (int i = 0; i < 10_000; i++)
        {
            var (success, roll) = RollRequirement.Impossible.Roll(rng);
            Assert.False(success);
            Assert.Equal(0, roll);
        }
    }

    [Fact]
    public void Roll_ReportsTheFirstDie_SoNaturalSixEffectsCanKeyOnIt()
    {
        // Killing Blow keys on a natural 6 to wound. On a compound requirement the first die
        // must be a 6 for the attempt to continue, so every success reports 6.
        var compound = RollRequirement.Compound(2);
        var rng = new Random(7);

        int successes = 0;
        for (int i = 0; i < 20_000; i++)
        {
            var (success, roll) = compound.Roll(rng);
            if (!success) continue;
            successes++;
            Assert.Equal(6, roll);
        }
        Assert.True(successes > 0, "expected some successes to inspect");
    }

    [Fact]
    public void Roll_NaturalOneAlwaysFails_EvenOnTheEasiestRequirement()
    {
        var rng = new Random(99);
        var easiest = RollRequirement.Plain(2);

        int failures = 0;
        for (int i = 0; i < 20_000; i++)
        {
            var (success, roll) = easiest.Roll(rng);
            if (!success)
            {
                Assert.Equal(1, roll);
                failures++;
            }
        }
        Assert.True(failures > 0, "a 2+ requirement should still fail on natural 1s");
    }

    [Fact]
    public void Compound_IsHarderThanPlainSix()
    {
        // The bug this type exists to fix: 6+/2+ was being treated as a flat 6+.
        Assert.True(RollRequirement.TryParse("6+", out var plain));
        Assert.True(RollRequirement.TryParse("6+/2+", out var compound));

        Assert.Equal(1.0 / 6.0, plain.Probability, 10);
        Assert.Equal(0.1389, compound.Probability, 4);
        Assert.True(compound.Probability < plain.Probability);
    }
}
