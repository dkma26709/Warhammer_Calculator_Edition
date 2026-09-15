using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The composition block the bundle carries, and the rules the validator derives from it.
/// </summary>
public class CompositionDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static CompositionData Load(string systemKey)
    {
        var path = Path.Combine(TestPaths.SystemDataDir(systemKey), $"{systemKey}.json");
        var bundle = JsonSerializer.Deserialize<BundleData>(File.ReadAllText(path), JsonOptions)!;
        Assert.NotNull(bundle.Composition);
        return bundle.Composition;
    }

    public static TheoryData<string> EnabledSystems()
    {
        var data = new TheoryData<string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
            data.Add(system.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void Composition_CarriesTheRequiredCategories(string systemKey)
    {
        var data = Load(systemKey);

        foreach (var required in new[] { "characters", "core", "special", "rare" })
            Assert.True(data.PointsCategories.ContainsKey(required), $"{systemKey} is missing '{required}'");

        Assert.NotNull(data.PointsCategories["characters"].MaxPercent);
        Assert.NotNull(data.PointsCategories["core"].MinPercent);
        Assert.NotNull(data.PointsCategories["special"].MaxPercent);
        Assert.NotNull(data.PointsCategories["rare"].MaxPercent);
    }

    [Fact]
    public void War_MergesCharacters_SoDeclaresNoLordsOrHeroesCaps()
    {
        var data = Load(GameSystems.War);

        Assert.False(data.PointsCategories.ContainsKey("lords"));
        Assert.False(data.PointsCategories.ContainsKey("heroes"));
        Assert.Equal(0.35, data.PointsCategories["characters"].MaxPercent);
        Assert.Equal(0.25, data.SingleUnitMaxPercent);
    }

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void EverySystem_CapsASingleUnit(string systemKey)
    {
        Assert.Equal(0.25, Load(systemKey).SingleUnitMaxPercent);
    }

    /// <summary>The hand-written test constants must not drift from the shipped bundle.</summary>
    [Fact]
    public void TestConstants_MatchTheShippedBundle()
    {
        var rules = CompositionRules.FromData(Load(GameSystems.War), null, GameSystems.War);
        var expected = TestCompositionRules.War;

        Assert.Equal(expected.LordsMaxPercent, rules.LordsMaxPercent);
        Assert.Equal(expected.HeroesMaxPercent, rules.HeroesMaxPercent);
        Assert.Equal(expected.CharactersCombinedMaxPercent, rules.CharactersCombinedMaxPercent);
        Assert.Equal(expected.SingleUnitMaxPercent, rules.SingleUnitMaxPercent);
        Assert.Equal(expected.CoreMinPercent, rules.CoreMinPercent);
        Assert.Equal(expected.SpecialMaxPercent, rules.SpecialMaxPercent);
        Assert.Equal(expected.RareMaxPercent, rules.RareMaxPercent);
        Assert.Equal(expected.GeneralRequired, rules.GeneralRequired);
        Assert.Equal(expected.BattleStandardMax, rules.BattleStandardMax);
        Assert.Equal(expected.SpecialCharactersMaxEach, rules.SpecialCharactersMaxEach);

        for (int points = 500; points <= 10_000; points += 500)
        {
            Assert.Equal(expected.SpecialDuplicates.LimitFor(points), rules.SpecialDuplicates.LimitFor(points));
            Assert.Equal(expected.RareDuplicates.LimitFor(points), rules.RareDuplicates.LimitFor(points));
        }
    }

    /// <summary>A faction's own constraints are appended to the system's, so the validator reads one list.</summary>
    [Fact]
    public async Task FactionRules_CarryTheBooksOwnConstraints()
    {
        var rules = await TestHelpers.Repo.GetCompositionRulesAsync("empire");

        Assert.Contains(rules.UnitConstraints, c => c.UnitIds.Contains("war-altar-of-sigmar") && c.Max == 1);
        Assert.Equal(0.35, rules.CharactersCombinedMaxPercent);
    }

    // ─── The duplicate chart ───────────────────────────────────────

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
    [InlineData(5999, 6, 4)]
    [InlineData(6000, 7, 5)]
    [InlineData(10_000, 11, 9)]
    public void DuplicateLimits_MatchTheBooksChart(int points, int expectedSpecial, int expectedRare)
    {
        var rules = TestCompositionRules.War;

        Assert.Equal(expectedSpecial, rules.SpecialDuplicates.LimitFor(points));
        Assert.Equal(expectedRare, rules.RareDuplicates.LimitFor(points));
    }

    [Fact]
    public void ARuleWithNoBands_IsUnlimited()
    {
        Assert.Equal(int.MaxValue, DuplicateRule.Unlimited.LimitFor(2000));
        Assert.Equal(int.MaxValue, DuplicateRule.FromData(null).LimitFor(2000));
    }
}

/// <summary>The single-unit cap, the merged characters section, and per-list overrides.</summary>
public class CompositionValidationTests
{
    private static readonly ArmyListValidator Validator = new();

    private static UnitConfiguration Entry(string name, UnitCategory category, double points, int count = 1)
    {
        var unit = TestHelpers.MakeUnit(name: name, points: points, category: category,
            troopType: category.IsCharacterCategory() ? "Infantry (Character)" : "Infantry");
        return new UnitConfiguration(unit) { ModelCount = count };
    }

    private static ArmyList List(int target, params UnitConfiguration[] units) => new()
    {
        FactionId = "dwarfs",
        PointsTarget = target,
        Units = units.ToList(),
    };

    [Fact]
    public void ASingleUnitOverTheCap_FlagsError()
    {
        var lord = Entry("Lord", UnitCategory.Characters, 600);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 10, 50));

        var issue = Assert.Single(Validator.Validate(list, TestCompositionRules.War),
            i => i.Code == "SINGLE_UNIT_OVER_CAP");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("Lord", issue.Message);
    }

    [Fact]
    public void TheSingleUnitCap_CoversRankAndFileToo()
    {
        var lord = Entry("Lord", UnitCategory.Characters, 100);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 10, 60));   // 600 > 500

        Assert.Contains(Validator.Validate(list, TestCompositionRules.War), i => i.Code == "SINGLE_UNIT_OVER_CAP");
    }

    [Fact]
    public void AListOverride_ReplacesTheSystemsPercentage()
    {
        var lord = Entry("Lord", UnitCategory.Characters, 100);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 10, 50),
            Entry("Cannon", UnitCategory.Rare, 400));
        list.Overrides = new CompositionOverrides
        {
            RareMax = new LimitOverride { Value = 15, Unit = LimitUnit.Percent },
        };

        var issue = Assert.Single(Validator.Validate(list, TestCompositionRules.War),
            i => i.Code == "RARE_OVER_CAP");
        Assert.Contains("15% of 2000", issue.Message);
    }

    [Fact]
    public void AFlatPointsOverride_IsIndependentOfTheTarget()
    {
        var rules = TestCompositionRules.War;
        var limits = rules.Resolve(new CompositionOverrides
        {
            CoreMin = new LimitOverride { Value = 600, Unit = LimitUnit.Points },
        }, 2000);

        Assert.Equal(600, limits.CoreMin.Points);
        Assert.Equal("600 pts", limits.CoreMin.Description);
        Assert.Equal(1000, limits.SpecialMax.Points);
    }

    [Fact]
    public void ADuplicateOverride_ReplacesTheChart()
    {
        var limits = TestCompositionRules.War.Resolve(
            new CompositionOverrides { SpecialDuplicateLimit = 1 }, 3000);

        Assert.Equal(1, limits.SpecialDuplicateLimit);
        Assert.Equal(2, limits.RareDuplicateLimit);
    }
}
