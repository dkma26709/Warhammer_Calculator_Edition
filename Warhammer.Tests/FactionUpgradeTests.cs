using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class FactionUpgradeTests
{
    [Fact]
    public void FactionUpgradeCategoryFor_BlessedSpawning_MapsToPlural()
    {
        Assert.Equal("blessed_spawnings",
            UnitConfiguration.FactionUpgradeCategoryFor(OptionType.BlessedSpawning));
    }

    [Fact]
    public void FactionUpgradeCategoryFor_Unknown_ReturnsNull()
    {
        Assert.Null(UnitConfiguration.FactionUpgradeCategoryFor(OptionType.Command));
        Assert.Null(UnitConfiguration.FactionUpgradeCategoryFor(null));
    }

    [Fact]
    public void SelectedFactionUpgrades_DefaultsToEmpty()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Empty(config.Loadout.SelectedFactionUpgrades);
    }

    [Fact]
    public void FactionUpgradeCost_PerModel_ScalesWithModelCountAndSelectionCount()
    {
        // 20 models × 1 pt/model × 2 spawnings selected = 40 pts
        var unit = TestHelpers.MakeUnit(points: 10, options: new List<UnitOption>
        {
            new() { Type = "blessed_spawning", Points = 1, PerModel = true },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] = new List<string>
        {
            "Blessed Spawning Of Chotec",
            "Blessed Spawning Of Sotek",
        };

        Assert.Equal(40, config.FactionUpgradeCost);
    }

    [Fact]
    public void FactionUpgradeCost_ZeroWhenNoneSelected()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new() { Type = "blessed_spawning", Points = 1, PerModel = true },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 20 };

        Assert.Equal(0, config.FactionUpgradeCost);
    }

    [Fact]
    public void TotalPointsCost_IncludesFactionUpgradeCost()
    {
        var unit = TestHelpers.MakeUnit(points: 10, options: new List<UnitOption>
        {
            new() { Type = "blessed_spawning", Points = 1, PerModel = true },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] = new List<string>
        {
            "Blessed Spawning Of Chotec",
        };

        // 20 × 10 (base) + 20 × 1 × 1 (spawning) = 220
        Assert.Equal(220, config.TotalPointsCost);
    }
}
