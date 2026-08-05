using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The shipped composition.json files, and the rules the validator derives from them.
/// </summary>
public class CompositionDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static CompositionData Load(string systemKey)
    {
        var path = Path.Combine(TestPaths.SystemDataDir(systemKey), "composition.json");
        Assert.True(File.Exists(path), $"{systemKey} has no composition.json");
        return JsonSerializer.Deserialize<CompositionData>(File.ReadAllText(path), JsonOptions)!;
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
    public void Composition_Deserializes_AndCarriesTheRequiredCategories(string systemKey)
    {
        var data = Load(systemKey);

        Assert.Equal(systemKey, data.System);
        Assert.InRange(data.CharactersCombinedMaxPercent, 0.01, 1.0);

        foreach (var required in new[] { "core", "special", "rare" })
            Assert.True(data.Categories.ContainsKey(required), $"{systemKey} is missing '{required}'");

        Assert.NotNull(data.Categories["core"].MinPercent);
        Assert.NotNull(data.Categories["special"].MaxPercent);
        Assert.NotNull(data.Categories["rare"].MaxPercent);
    }

    [Fact]
    public void WapV2_SplitsCharactersIntoLordsAndHeroes()
    {
        var data = Load("wap-v2");

        Assert.Equal(0.25, data.Categories["lords"].MaxPercent);
        Assert.Equal(0.25, data.Categories["heroes"].MaxPercent);
        Assert.Equal(0.35, data.CharactersCombinedMaxPercent);
    }

    [Fact]
    public void WapV3_MergesCharacters_SoDeclaresNoLordsOrHeroesCaps()
    {
        var data = Load("wap-v3");

        Assert.False(data.Categories.ContainsKey("lords"));
        Assert.False(data.Categories.ContainsKey("heroes"));
        Assert.Equal(0.35, data.CharactersCombinedMaxPercent);
        Assert.Equal(0.25, data.SingleCharacterMaxPercent);
    }

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void EverySystem_CapsASingleCharacter(string systemKey)
    {
        // DKM confirmed this applies to both v2 and v3, so no shipped system may omit it.
        Assert.Equal(0.25, Load(systemKey).SingleCharacterMaxPercent);
    }

    /// <summary>The hand-written test constants must not drift from the shipped files.</summary>
    [Theory]
    [InlineData("wap-v2")]
    [InlineData("wap-v3")]
    public void TestConstants_MatchTheShippedFiles(string systemKey)
    {
        var data = Load(systemKey);
        var expected = systemKey == "wap-v2" ? TestCompositionRules.WapV2 : TestCompositionRules.WapV3;

        Assert.Equal(expected.LordsMaxPercent,
            data.Categories.TryGetValue("lords", out var l) ? l.MaxPercent : null);
        Assert.Equal(expected.HeroesMaxPercent,
            data.Categories.TryGetValue("heroes", out var h) ? h.MaxPercent : null);
        Assert.Equal(expected.CharactersCombinedMaxPercent, data.CharactersCombinedMaxPercent);
        Assert.Equal(expected.SingleCharacterMaxPercent, data.SingleCharacterMaxPercent);
        Assert.Equal(expected.CoreMinPercent, data.Categories["core"].MinPercent);
        Assert.Equal(expected.SpecialMaxPercent, data.Categories["special"].MaxPercent);
        Assert.Equal(expected.RareMaxPercent, data.Categories["rare"].MaxPercent);
    }
}

/// <summary>The single-character cap, v3's merged section, and per-list overrides.</summary>
public class CompositionValidationTests
{
    private static readonly ArmyListValidator Validator = new();

    private static UnitConfiguration Entry(string name, UnitCategory category, double points, int count = 1)
    {
        var unit = TestHelpers.MakeUnit(name: name, points: points);
        unit = unit with
        {
            Category = category,
            RawEntry = unit.RawEntry with { Category = category.ToJsonKey() },
        };
        return new UnitConfiguration(unit) { ModelCount = count };
    }

    private static ArmyList List(int target, params UnitConfiguration[] units) => new()
    {
        Faction = FactionKey.Dwarfs,
        PointsTarget = target,
        Units = units.ToList(),
    };

    private static List<string> Codes(ArmyList list, CompositionRules rules) =>
        Validator.Validate(list, rules).Select(i => i.Code).ToList();

    private static LimitOverride Pct(double value) => new() { Value = value, Unit = LimitUnit.Percent };
    private static LimitOverride Pts(double value) => new() { Value = value, Unit = LimitUnit.Points };

    // ─── Single-character cap ───────────────────────────────────────

    [Fact]
    public void SingleCharacter_OverQuarterOfTarget_IsAnError()
    {
        var lord = Entry("Overpriced Lord", UnitCategory.Lords, 501);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        Assert.Contains("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void SingleCharacter_ExactlyAtTheCap_IsAllowed()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 500);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        Assert.DoesNotContain("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void SingleCharacter_CapNamesTheOffendingCharacter()
    {
        var lord = Entry("Overpriced Lord", UnitCategory.Lords, 600);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        var issue = Validator.Validate(list, TestCompositionRules.WapV2)
            .Single(i => i.Code == "SINGLE_CHARACTER_OVER_CAP");

        Assert.Same(lord, issue.RelatedUnit);
        Assert.Contains("Overpriced Lord", issue.Message);
    }

    [Fact]
    public void TwoCharactersEachUnderTheCap_DoNotTripIt()
    {
        // 400 + 400 = 800, over the 700 combined cap but neither is over 500 alone.
        var lord = Entry("Lord", UnitCategory.Lords, 400);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Thane", UnitCategory.Heroes, 400),
            Entry("Warriors", UnitCategory.Core, 500));

        var codes = Codes(list, TestCompositionRules.WapV2);

        Assert.DoesNotContain("SINGLE_CHARACTER_OVER_CAP", codes);
        Assert.Contains("CHARACTERS_OVER_CAP", codes);
    }

    [Fact]
    public void SingleCharacter_CapAppliesToTheMergedV3Category()
    {
        var character = Entry("Verminlord", UnitCategory.Characters, 600);
        character.Loadout.IsGeneral = true;
        var list = List(2000, character, Entry("Clanrats", UnitCategory.Core, 500));

        Assert.Contains("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV3));
    }

    // ─── v3's merged Characters section ─────────────────────────────

    [Fact]
    public void V3_HasNoLordsOrHeroesCaps()
    {
        // 600 in Lords would breach v2's 25% sub-cap; v3 declares no such limit.
        var lord = Entry("Grey Seer", UnitCategory.Lords, 400);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Thane", UnitCategory.Heroes, 200),
            Entry("Clanrats", UnitCategory.Core, 500));

        var codes = Codes(list, TestCompositionRules.WapV3);

        Assert.DoesNotContain("LORDS_OVER_CAP", codes);
        Assert.DoesNotContain("HEROES_OVER_CAP", codes);
    }

    [Fact]
    public void V3_StillEnforcesTheCombinedCharactersCap()
    {
        var character = Entry("Verminlord", UnitCategory.Characters, 450);
        character.Loadout.IsGeneral = true;
        var list = List(2000, character, Entry("Warlock Master", UnitCategory.Characters, 300),
            Entry("Clanrats", UnitCategory.Core, 500));

        // 750 > 700 (35% of 2000)
        Assert.Contains("CHARACTERS_OVER_CAP", Codes(list, TestCompositionRules.WapV3));
    }

    [Fact]
    public void V2_StillEnforcesTheLordsSubCap()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 600);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        Assert.Contains("LORDS_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    // ─── Per-list overrides ─────────────────────────────────────────

    [Fact]
    public void RareOverride_TightensTheCap()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Gyrocopter", UnitCategory.Rare, 400));

        // 400 is inside the stock 25% (500) but outside a house 15% (300).
        Assert.DoesNotContain("RARE_OVER_CAP", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { RareMax = Pct(15) };
        Assert.Contains("RARE_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void SpecialOverride_CanLoosenTheCap()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Hammerers", UnitCategory.Special, 1100));

        Assert.Contains("SPECIAL_OVER_CAP", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { SpecialMax = Pct(60) };
        Assert.DoesNotContain("SPECIAL_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void CoreMinimumOverride_IsHonoured()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 300));

        // 300 is under the stock 25% floor (500) but clears a relaxed 10% (200).
        Assert.Contains("CORE_BELOW_MIN", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { CoreMin = Pct(10) };
        Assert.DoesNotContain("CORE_BELOW_MIN", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void SingleCharacterOverride_IsHonoured()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 450);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        Assert.DoesNotContain("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { SingleCharacterMax = Pct(20) };
        Assert.Contains("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void DuplicateLimitOverride_ReplacesThePointsBracketFigure()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000,
            lord,
            Entry("Warriors", UnitCategory.Core, 500),
            Entry("Hammerers", UnitCategory.Special, 100),
            Entry("Hammerers", UnitCategory.Special, 100));

        // 2000 pts allows 3 Special duplicates, so two is fine by default.
        Assert.DoesNotContain("DUPLICATE_LIMIT_EXCEEDED", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { SpecialDuplicateLimit = 1 };
        Assert.Contains("DUPLICATE_LIMIT_EXCEEDED", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void LordsOverride_AppliesEvenWhereTheSystemDeclaresNoCap()
    {
        // A house ruleset may reintroduce a sub-cap v3 does not have.
        var lord = Entry("Grey Seer", UnitCategory.Lords, 400);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Clanrats", UnitCategory.Core, 500));

        Assert.DoesNotContain("LORDS_OVER_CAP", Codes(list, TestCompositionRules.WapV3));

        list.Overrides = new CompositionOverrides { LordsMax = Pct(10) };
        Assert.Contains("LORDS_OVER_CAP", Codes(list, TestCompositionRules.WapV3));
    }

    [Fact]
    public void NoOverrides_ResolveToTheSystemPercentages()
    {
        var limits = TestCompositionRules.WapV2.Resolve(new CompositionOverrides(), 2000);

        Assert.Equal(500, limits.LordsMax!.Points);
        Assert.Equal(700, limits.CharactersCombinedMax.Points);
        Assert.Equal(500, limits.CoreMin.Points);
        Assert.Equal(1000, limits.SpecialMax.Points);
        Assert.Equal(500, limits.RareMax.Points);
        Assert.Equal(500, limits.SingleCharacterMax!.Points);
        Assert.Equal(3, limits.SpecialDuplicateLimit);
        Assert.Equal(1, limits.RareDuplicateLimit);
        Assert.True(new CompositionOverrides().IsEmpty);
    }

    // ─── Fixed points overrides ─────────────────────────────────────

    [Fact]
    public void PointsOverride_CapsAtAFlatFigure()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Gyrocopter", UnitCategory.Rare, 400));

        // 400 clears the stock 25% (500) but breaches a flat 300.
        Assert.DoesNotContain("RARE_OVER_CAP", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { RareMax = Pts(300) };
        Assert.Contains("RARE_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void PointsOverride_DoesNotScaleWithThePointsTarget()
    {
        var overrides = new CompositionOverrides { RareMax = Pts(300) };

        Assert.Equal(300, TestCompositionRules.WapV2.Resolve(overrides, 1000).RareMax.Points);
        Assert.Equal(300, TestCompositionRules.WapV2.Resolve(overrides, 2000).RareMax.Points);
        Assert.Equal(300, TestCompositionRules.WapV2.Resolve(overrides, 6000).RareMax.Points);
    }

    [Fact]
    public void PercentOverride_ScalesWithThePointsTarget()
    {
        var overrides = new CompositionOverrides { RareMax = Pct(15) };

        Assert.Equal(150, TestCompositionRules.WapV2.Resolve(overrides, 1000).RareMax.Points);
        Assert.Equal(300, TestCompositionRules.WapV2.Resolve(overrides, 2000).RareMax.Points);
        Assert.Equal(900, TestCompositionRules.WapV2.Resolve(overrides, 6000).RareMax.Points);
    }

    [Fact]
    public void PointsOverride_WorksForAMinimumToo()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 300));

        Assert.Contains("CORE_BELOW_MIN", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { CoreMin = Pts(250) };
        Assert.DoesNotContain("CORE_BELOW_MIN", Codes(list, TestCompositionRules.WapV2));
    }

    [Fact]
    public void PointsOverride_CapsASingleCharacter()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 450);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500));

        Assert.DoesNotContain("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));

        list.Overrides = new CompositionOverrides { SingleCharacterMax = Pts(400) };
        Assert.Contains("SINGLE_CHARACTER_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Theory]
    [InlineData(800)]
    [InlineData(2500)]
    [InlineData(100000)]
    public void PointsOverride_IsNotClampedToOneHundred(double points)
    {
        var limits = TestCompositionRules.WapV2.Resolve(
            new CompositionOverrides { RareMax = Pts(points) }, 2000);

        Assert.Equal(points, limits.RareMax.Points);
    }

    [Fact]
    public void PercentOverride_AboveOneHundred_IsAllowed()
    {
        // A house ruleset is entitled to say "Special up to 120%".
        var limits = TestCompositionRules.WapV2.Resolve(
            new CompositionOverrides { SpecialMax = Pct(120) }, 2000);

        Assert.Equal(2400, limits.SpecialMax.Points);
    }

    [Fact]
    public void PointsOverride_LargerThanTheTarget_StillResolves()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Gyrocopter", UnitCategory.Rare, 800));
        list.Overrides = new CompositionOverrides { RareMax = Pts(900) };

        Assert.DoesNotContain("RARE_OVER_CAP", Codes(list, TestCompositionRules.WapV2));
    }

    [Theory]
    [InlineData(15, LimitUnit.Percent, "15% of 2000")]
    [InlineData(300, LimitUnit.Points, "300 pts")]
    public void Messages_NameTheSourceOfTheLimit(double value, LimitUnit unit, string expected)
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Gyrocopter", UnitCategory.Rare, 400));
        list.Overrides = new CompositionOverrides { RareMax = new() { Value = value, Unit = unit } };

        var message = Validator.Validate(list, TestCompositionRules.WapV2)
            .Single(i => i.Code == "RARE_OVER_CAP").Message;

        Assert.Contains(expected, message);
    }

    [Fact]
    public void SystemLimit_MessageStillReadsAsAPercentage()
    {
        var lord = Entry("Lord", UnitCategory.Lords, 200);
        lord.Loadout.IsGeneral = true;
        var list = List(2000, lord, Entry("Warriors", UnitCategory.Core, 500),
            Entry("Gyrocopter", UnitCategory.Rare, 600));

        var message = Validator.Validate(list, TestCompositionRules.WapV2)
            .Single(i => i.Code == "RARE_OVER_CAP").Message;

        Assert.Contains("25% of 2000", message);
    }
}
