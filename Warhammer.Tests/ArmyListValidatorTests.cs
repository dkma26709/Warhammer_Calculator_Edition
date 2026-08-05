using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

public class ArmyListValidatorTests
{
    private static readonly ArmyListValidatorInvoker V = new();

    // ─── Helpers ────────────────────────────────────────────────────

    private static UnitConfiguration MakeEntry(
        string name,
        UnitCategory category,
        double pointsPerModel,
        int count = 1,
        string? notes = null)
    {
        var unit = TestHelpers.MakeUnit(name: name, points: pointsPerModel);
        unit = unit with
        {
            Category = category,
            RawEntry = unit.RawEntry with
            {
                Category = category.ToJsonKey(),
                Notes = notes,
            },
        };
        return new UnitConfiguration(unit) { ModelCount = count };
    }

    private static ArmyList MakeList(int target, params UnitConfiguration[] units) => new()
    {
        Faction = FactionKey.Dwarfs,
        PointsTarget = target,
        Units = units.ToList(),
    };

    // ─── Happy path ─────────────────────────────────────────────────

    [Fact]
    public void ValidList_ReturnsNoIssues()
    {
        var lord = MakeEntry("Lord", UnitCategory.Lords, 200, 1);
        lord.Loadout.IsGeneral = true;
        var list = MakeList(2000,
            lord,                                                    // 200 (10%)
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),       // 600 (30%)
            MakeEntry("Ironbreakers", UnitCategory.Special, 15, 20) // 300 (15%)
        );

        var issues = V.Validate(list);

        Assert.Empty(issues);
    }

    [Fact]
    public void NoGeneralDesignated_WithCharacter_WarnsNotErrors()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var issues = V.Validate(list);

        var general = Assert.Single(issues, i => i.Code == "NO_GENERAL_DESIGNATED");
        Assert.Equal(ValidationSeverity.Warning, general.Severity);
        Assert.DoesNotContain(issues, i => i.Code == "NO_GENERAL");
    }

    [Fact]
    public void MultipleGenerals_FlagsError()
    {
        var lord = MakeEntry("Lord", UnitCategory.Lords, 200, 1);
        lord.Loadout.IsGeneral = true;
        var thane = MakeEntry("Thane", UnitCategory.Heroes, 100, 1);
        thane.Loadout.IsGeneral = true;
        var list = MakeList(2000, lord, thane, MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("MULTIPLE_GENERALS", codes);
    }

    [Fact]
    public void MultipleBsb_FlagsError()
    {
        var lord = MakeEntry("Lord", UnitCategory.Lords, 200, 1);
        lord.Loadout.IsGeneral = true;
        lord.Loadout.IsBattleStandardBearer = true;
        var thane = MakeEntry("Thane", UnitCategory.Heroes, 100, 1);
        thane.Loadout.IsBattleStandardBearer = true;
        var list = MakeList(2000, lord, thane, MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("MULTIPLE_BSB", codes);
    }

    // ─── Invalid target ─────────────────────────────────────────────

    [Fact]
    public void ZeroPointsTarget_ReturnsOnlyInvalidTargetIssue()
    {
        var list = MakeList(0);

        var issues = V.Validate(list);

        Assert.Single(issues);
        Assert.Equal("INVALID_TARGET", issues[0].Code);
    }

    // ─── Percentage caps ────────────────────────────────────────────

    [Fact]
    public void LordsOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 600, 1),          // 600 > 500 cap
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("LORDS_OVER_CAP", codes);
    }

    [Fact]
    public void HeroesOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Thane", UnitCategory.Heroes, 600, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("HEROES_OVER_CAP", codes);
    }

    [Fact]
    public void CharactersCombinedOverCap_FlagsError()
    {
        // 400 Lords + 400 Heroes = 800 total > 700 cap (35% of 2000).
        // Both individual caps (25% = 500) are respected.
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 400, 1),
            MakeEntry("Thane", UnitCategory.Heroes, 400, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CHARACTERS_OVER_CAP", codes);
        Assert.DoesNotContain("LORDS_OVER_CAP", codes);
        Assert.DoesNotContain("HEROES_OVER_CAP", codes);
    }

    [Fact]
    public void CoreBelowMin_FlagsError()
    {
        // 400 Core at 2000 target → below 500 (25%) min
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 40));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CORE_BELOW_MIN", codes);
    }

    [Fact]
    public void SpecialOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Ironbreakers", UnitCategory.Special, 15, 80)); // 1200 > 1000

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("SPECIAL_OVER_CAP", codes);
    }

    [Fact]
    public void RareOverCap_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Cannon", UnitCategory.Rare, 600, 1)); // 600 > 500

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("RARE_OVER_CAP", codes);
    }

    // ─── General requirement ────────────────────────────────────────

    [Fact]
    public void NoCharacter_FlagsNoGeneral()
    {
        var list = MakeList(2000,
            MakeEntry("Warriors", UnitCategory.Core, 10, 60));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("NO_GENERAL", codes);
    }

    [Fact]
    public void EmptyList_FlagsCoreMinAndNoGeneral()
    {
        var list = MakeList(2000);

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("CORE_BELOW_MIN", codes);
        Assert.Contains("NO_GENERAL", codes);
    }

    // ─── Total cap ──────────────────────────────────────────────────

    [Fact]
    public void OverPointsTarget_FlagsError()
    {
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 100)); // 1000 + 200 = 1200 > 1000

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("OVER_POINTS_TARGET", codes);
    }

    // ─── 0-X per army ───────────────────────────────────────────────

    [Fact]
    public void PerArmyLimit_Exceeded_FlagsError()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1, notes: "0-1 per army"),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1, notes: "0-1 per army"));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("PER_ARMY_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void PerArmyLimit_AtLimit_DoesNotFlag()
    {
        var list = MakeList(2000,
            MakeEntry("Lord", UnitCategory.Lords, 200, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 60),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1, notes: "0-2 per army"),
            MakeEntry("Shrine", UnitCategory.Rare, 200, 1, notes: "0-2 per army"));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.DoesNotContain("PER_ARMY_LIMIT_EXCEEDED", codes);
    }

    // ─── Duplicate limits ───────────────────────────────────────────

    [Fact]
    public void SpecialDuplicates_AboveBracket_FlagsError()
    {
        // At 1500 target, Special cap = 2. Three copies of the same Special triggers error.
        var list = MakeList(1500,
            MakeEntry("Lord", UnitCategory.Lords, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 50),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10),
            MakeEntry("Hammerers", UnitCategory.Special, 12, 10));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void RareDuplicates_AboveBracket_FlagsError()
    {
        // At 1000 target, Rare cap = 1. Two copies triggers error.
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Lords, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 40),
            MakeEntry("Cannon", UnitCategory.Rare, 100, 1),
            MakeEntry("Cannon", UnitCategory.Rare, 100, 1));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.Contains("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    [Fact]
    public void SpecialDuplicates_DifferentNames_AllowedBeyondCap()
    {
        // Many different Specials is fine — cap is per unit *name*, not total count.
        var list = MakeList(1000,
            MakeEntry("Lord", UnitCategory.Lords, 150, 1),
            MakeEntry("Warriors", UnitCategory.Core, 10, 30),
            MakeEntry("Hammerers", UnitCategory.Special, 100, 1),
            MakeEntry("Ironbreakers", UnitCategory.Special, 100, 1),
            MakeEntry("Miners", UnitCategory.Special, 100, 1));

        var codes = V.Validate(list).Select(i => i.Code).ToList();

        Assert.DoesNotContain("DUPLICATE_LIMIT_EXCEEDED", codes);
    }

    // Simple wrapper so tests aren't littered with `new ArmyListValidator()`.
    private class ArmyListValidatorInvoker
    {
        private readonly Warhammer.Services.ArmyListValidator _inner = new();

        public List<ValidationIssue> Validate(ArmyList l) => _inner.Validate(l, TestCompositionRules.WapV2);
        public List<ValidationIssue> Validate(ArmyList l, CompositionRules rules) => _inner.Validate(l, rules);
    }
}

/// <summary>
/// Percentages mirroring the shipped composition.json files, so validator tests don't need
/// an HTTP round trip. Kept in step with <c>wwwroot/data/{system}/composition.json</c> by
/// <see cref="CompositionDataTests"/>.
/// </summary>
internal static class TestCompositionRules
{
    public static readonly CompositionRules WapV2 = new()
    {
        LordsMaxPercent = 0.25,
        HeroesMaxPercent = 0.25,
        CharactersCombinedMaxPercent = 0.35,
        SingleCharacterMaxPercent = 0.25,
        CoreMinPercent = 0.25,
        SpecialMaxPercent = 0.50,
        RareMaxPercent = 0.25,
    };

    /// <summary>v3 merges Lords and Heroes, so it declares no per-section character caps.</summary>
    public static readonly CompositionRules WapV3 = WapV2 with
    {
        LordsMaxPercent = null,
        HeroesMaxPercent = null,
    };
}

public class ArmyCompositionRulesTests
{
    [Theory]
    [InlineData(500, 1, 1)]
    [InlineData(999, 1, 1)]
    [InlineData(1000, 2, 1)]
    [InlineData(1999, 2, 1)]
    [InlineData(2000, 3, 1)]
    [InlineData(2500, 3, 1)]
    [InlineData(2999, 3, 1)]
    [InlineData(3000, 4, 2)]
    [InlineData(3999, 4, 2)]
    [InlineData(4000, 5, 3)]
    [InlineData(4999, 5, 3)]
    [InlineData(5000, 6, 4)]
    [InlineData(6000, 7, 5)]
    [InlineData(10_000, 11, 9)]
    public void DuplicateLimits_MatchBracketTable(int points, int expectedSpecial, int expectedRare)
    {
        var (special, rare) = ArmyCompositionRules.DuplicateLimits(points);
        Assert.Equal(expectedSpecial, special);
        Assert.Equal(expectedRare, rare);
    }
}
