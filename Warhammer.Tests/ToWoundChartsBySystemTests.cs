using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The shipped to-wound chart as the engine actually sees it. The chart carries compound
/// results — "6+/2+", a six followed by a second roll — and cells no attack can wound on.
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

    [Theory]
    [InlineData(1, 4, "6+/2+")]
    [InlineData(1, 8, "6+/6+")]
    [InlineData(3, 6, "6+/2+")]
    [InlineData(4, 7, "6+/2+")]
    [InlineData(10, 10, "4+")]
    [InlineData(3, 3, "4+")]
    public void TheChart_CarriesTheBooksNotation(int strength, int toughness, string expected)
    {
        var requirement = LoadFor(GameSystems.War).GetToWoundRequirement(strength, toughness);
        Assert.Equal(expected, requirement.ToString());
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    public void TheChart_MarksTheImpossibleCellsImpossible(int strength, int toughness)
    {
        var requirement = LoadFor(GameSystems.War).GetToWoundRequirement(strength, toughness);

        Assert.False(requirement.CanSucceed);
        Assert.Equal(0.0, requirement.Probability);
    }

    [Fact]
    public void CompoundCells_AreHarderThanAFlatSix()
    {
        // Stored as a flat 6+, these cells would make weak attacks against tough targets more
        // effective than the rules allow.
        var requirement = LoadFor(GameSystems.War).GetToWoundRequirement(1, 4);

        Assert.True(requirement.NeedsSecondRoll);
        Assert.True(requirement.Probability < 1.0 / 6.0);
        Assert.Equal(0.1389, requirement.Probability, 4);
    }

    [Fact]
    public void TheToHitChart_IsPlainScoresThroughout()
    {
        var service = LoadFor(GameSystems.War);

        for (int a = 1; a <= 10; a++)
        {
            for (int d = 1; d <= 10; d++)
            {
                var requirement = service.GetToHitRequirement(a, d);
                Assert.True(requirement.CanSucceed, $"WS{a} vs WS{d} should be reachable");
                Assert.False(requirement.NeedsSecondRoll, $"WS{a} vs WS{d} should be a plain score");
            }
        }
    }
}
