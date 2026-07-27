using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class GeneralAndBsbTests
{
    [Fact]
    public void IsBattleStandardEligible_FalseWithoutOption()
    {
        var unit = TestHelpers.MakeUnit();
        var config = new UnitConfiguration(unit);

        Assert.False(config.IsBattleStandardEligible);
    }

    [Fact]
    public void IsBattleStandardEligible_TrueWhenOptionPresent()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new() { Type = "battle_standard", Points = 25 },
        });
        var config = new UnitConfiguration(unit);

        Assert.True(config.IsBattleStandardEligible);
    }

    [Fact]
    public void BattleStandardCost_ZeroWhenNotBsb()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new() { Type = "battle_standard", Points = 25 },
        });
        var config = new UnitConfiguration(unit);

        Assert.Equal(0, config.BattleStandardCost);
    }

    [Fact]
    public void BattleStandardCost_AddsOptionPointsWhenBsb()
    {
        var unit = TestHelpers.MakeUnit(points: 100, options: new List<UnitOption>
        {
            new() { Type = "battle_standard", Points = 25 },
        });
        var config = new UnitConfiguration(unit) { Loadout = { IsBattleStandardBearer = true } };

        Assert.Equal(25, config.BattleStandardCost);
    }

    [Fact]
    public void TotalPointsCost_IncludesBattleStandardCost()
    {
        var unit = TestHelpers.MakeUnit(points: 100, options: new List<UnitOption>
        {
            new() { Type = "battle_standard", Points = 25 },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 1, Loadout = { IsBattleStandardBearer = true } };

        Assert.Equal(100 + 25, config.TotalPointsCost);
    }

    [Fact]
    public void IsGeneral_DefaultsToFalse()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.False(config.Loadout.IsGeneral);
    }

    [Fact]
    public void IsBattleStandardBearer_DefaultsToFalse()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.False(config.Loadout.IsBattleStandardBearer);
    }
}
