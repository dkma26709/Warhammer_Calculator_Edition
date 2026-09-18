using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

public class MagicBannerTests
{
    [Fact]
    public void BannerBudget_Null_WhenNoStandardBearerOption()
    {
        var unit = TestHelpers.MakeUnit();
        var config = new UnitConfiguration(unit) { HasStandardBearer = true };

        Assert.Null(config.BannerBudget);
        Assert.Null(config.MagicStandardAllowance);
    }

    [Fact]
    public void BannerBudget_Null_WhenStandardBearerHasNoMagicStandardUnderIt()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.Command(CommandRole.StandardBearer, 10)]);
        var config = new UnitConfiguration(unit) { HasStandardBearer = true };

        Assert.Null(config.BannerBudget);
    }

    [Fact]
    public void BannerBudget_Null_UntilTheStandardBearerIsTaken()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.Command(CommandRole.StandardBearer, 10, bannerCap: 25)]);
        var config = new UnitConfiguration(unit);

        Assert.Null(config.BannerBudget);

        config.HasStandardBearer = true;
        Assert.Equal(25, config.BannerBudget);
    }

    [Fact]
    public void BannerBudget_MatchesThePointsLimit_UnderTheStandardBearer()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.Command(CommandRole.StandardBearer, 10, bannerCap: 25)]);
        var config = new UnitConfiguration(unit) { HasStandardBearer = true };

        Assert.Equal(25, config.BannerBudget);
        Assert.Equal(25, config.MagicStandardAllowance!.PointsLimit);
    }

    [Fact]
    public void AnUnlimitedAllowance_HasNoPointsLimit()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.Command(CommandRole.StandardBearer, 10, bannerUnlimited: true)]);
        var config = new UnitConfiguration(unit) { HasStandardBearer = true };

        Assert.Null(config.MagicStandardAllowance!.PointsLimit);
        Assert.Equal(int.MaxValue, config.BannerBudget);
    }

    [Fact]
    public void ACharacter_TakesItsBannerThroughTheBattleStandard()
    {
        var banner = new Models.Json.OptionGroup
        {
            Id = "magic-standard", Kind = "magicStandard", Select = "any", Min = 0, Max = 1, PointsLimit = null,
            Raw = "The Battle Standard Bearer can have a Magic Standard with no points limit",
            Choices = [new Models.Json.OptionChoice { Id = "magic-standard", Name = "Magic Standard", Raw = "Magic Standard" }],
        };
        var unit = TestHelpers.MakeUnit(troopType: "Infantry (Character)", options: [TestHelpers.BattleStandard(25), banner]);
        var config = new UnitConfiguration(unit);

        Assert.Null(config.MagicStandardAllowance);

        config.Loadout.IsBattleStandardBearer = true;
        Assert.NotNull(config.MagicStandardAllowance);
        Assert.Null(config.MagicStandardAllowance.PointsLimit);
    }

    [Fact]
    public void TotalPointsCost_IncludesBannerCost()
    {
        var unit = TestHelpers.MakeUnit(points: 10);
        var config = new UnitConfiguration(unit)
        {
            ModelCount = 20,
            Loadout =
            {
                SelectedBanner = new SelectedMagicItem
                {
                    Name = "Razor Standard",
                    Category = MagicItemCategories.Standard,
                    PointsCost = 45,
                },
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
