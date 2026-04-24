using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class MagicBannerTests
{
    [Fact]
    public void BannerBudget_Null_WhenNoStandardBearerOption()
    {
        var unit = TestHelpers.MakeUnit();
        var config = new UnitConfiguration(unit);

        Assert.Null(config.BannerBudget);
    }

    [Fact]
    public void BannerBudget_Null_WhenStandardBearerHasNoMagicStandardSubOption()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new()
            {
                Type = "command",
                Role = "Standard Bearer",
                Points = 10,
            },
        });
        var config = new UnitConfiguration(unit);

        Assert.Null(config.BannerBudget);
    }

    [Fact]
    public void BannerBudget_MatchesPointsLimit_FromMagicStandardSubOption()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new()
            {
                Type = "command",
                Role = "Standard Bearer",
                Points = 10,
                SubOptions = new List<UnitOption>
                {
                    new() { Type = "magic_standard", PointsLimit = 25 },
                },
            },
        });
        var config = new UnitConfiguration(unit);

        Assert.Equal(25, config.BannerBudget);
    }

    [Fact]
    public void TotalPointsCost_IncludesBannerCost()
    {
        var unit = TestHelpers.MakeUnit(points: 10);
        var config = new UnitConfiguration(unit)
        {
            ModelCount = 20,
            SelectedBanner = new SelectedMagicItem
            {
                Name = "Razor Standard",
                Category = "magic_standard",
                PointsCost = 45,
            },
        };

        Assert.Equal(200 + 45, config.TotalPointsCost);
    }

    [Fact]
    public void TotalPointsCost_WithoutBanner_Unchanged()
    {
        var unit = TestHelpers.MakeUnit(points: 10);
        var config = new UnitConfiguration(unit) { ModelCount = 20 };

        Assert.Equal(200, config.TotalPointsCost);
    }
}
