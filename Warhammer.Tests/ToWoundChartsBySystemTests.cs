using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The shipped charts as the engine actually sees them, per system. wap-v3's to-wound chart
/// carries compound results and immunity; wap-v2's does not.
/// </summary>
public class ToWoundChartsBySystemTests
{
    private static CoreRulesService LoadFor(string systemKey)
    {
        var wwwroot = Path.Combine(TestHelpers.FindProjectRoot(), "wwwroot");
        var http = new HttpClient(new FileBasedHttpHandler(wwwroot))
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        var system = new GameSystemService();
        system.Initialize(systemKey);
        Assert.Equal(systemKey, system.CurrentSystem);

        var service = new CoreRulesService(http, system);
        service.EnsureLoadedAsync().GetAwaiter().GetResult();
        return service;
    }

    // ─── wap-v3: the chart this whole type exists for ──────────────

    [Theory]
    [InlineData(1, 4, "6+/2+")]
    [InlineData(1, 8, "6+/6+")]
    [InlineData(3, 6, "6+/2+")]
    [InlineData(4, 7, "6+/2+")]
    [InlineData(10, 10, "4+")]
    [InlineData(3, 3, "4+")]
    public void WapV3_CarriesTheBooksNotation(int strength, int toughness, string expected)
    {
        var requirement = LoadFor("wap-v3").GetToWoundRequirement(strength, toughness);
        Assert.Equal(expected, requirement.ToString());
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    public void WapV3_MarksTheImpossibleCellsImpossible(int strength, int toughness)
    {
        var requirement = LoadFor("wap-v3").GetToWoundRequirement(strength, toughness);

        Assert.False(requirement.CanSucceed);
        Assert.Equal(0.0, requirement.Probability);
    }

    [Fact]
    public void WapV3_CompoundCellsAreHarderThanTheFlatSixTheyReplaced()
    {
        // Before this work these cells were stored as a flat 6+, which made weak attacks
        // against tough targets more effective than the rules allow.
        var requirement = LoadFor("wap-v3").GetToWoundRequirement(1, 4);

        Assert.True(requirement.NeedsSecondRoll);
        Assert.True(requirement.Probability < 1.0 / 6.0);
        Assert.Equal(0.1389, requirement.Probability, 4);
    }

    // ─── wap-v2: unchanged by any of this ──────────────────────────

    [Fact]
    public void WapV2_ToWoundIsEntirelyPlainScores()
    {
        var service = LoadFor("wap-v2");

        for (int s = 1; s <= 10; s++)
        {
            for (int t = 1; t <= 10; t++)
            {
                var requirement = service.GetToWoundRequirement(s, t);
                Assert.True(requirement.CanSucceed, $"wap-v2 S{s} vs T{t} should be reachable");
                Assert.False(requirement.NeedsSecondRoll,
                    $"wap-v2 S{s} vs T{t} should be a single roll, got {requirement}");
            }
        }
    }

    [Theory]
    [InlineData("wap-v2")]
    [InlineData("wap-v3")]
    public void ToHitIsPlainInEverySystem(string systemKey)
    {
        var service = LoadFor(systemKey);

        for (int attacker = 1; attacker <= 10; attacker++)
        {
            for (int defender = 1; defender <= 10; defender++)
            {
                var requirement = service.GetToHitRequirement(attacker, defender);
                Assert.True(requirement.CanSucceed);
                Assert.False(requirement.NeedsSecondRoll);
            }
        }
    }

    [Theory]
    [InlineData("wap-v2")]
    [InlineData("wap-v3")]
    public void ArmourSaveModifiersAreLoadedForEverySystem(string systemKey)
    {
        var service = LoadFor(systemKey);

        Assert.Equal(0, service.GetArmourSaveModifier(3));
        Assert.Equal(-1, service.GetArmourSaveModifier(4));
        Assert.Equal(-2, service.GetArmourSaveModifier(5));
        Assert.Equal(-3, service.GetArmourSaveModifier(6));
        Assert.Equal(-7, service.GetArmourSaveModifier(10));
    }
}
