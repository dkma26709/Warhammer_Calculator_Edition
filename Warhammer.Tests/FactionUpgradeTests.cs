using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class FactionUpgradeTests
{
    [Fact]
    public void FactionUpgradeCategoryForType_BlessedSpawning_MapsToPlural()
    {
        Assert.Equal("blessed_spawnings",
            UnitConfiguration.FactionUpgradeCategoryForType(OptionType.BlessedSpawning));
    }

    [Fact]
    public void FactionUpgradeCategoryForType_Unknown_ReturnsNull()
    {
        Assert.Null(UnitConfiguration.FactionUpgradeCategoryForType(OptionType.Command));
        Assert.Null(UnitConfiguration.FactionUpgradeCategoryForType(null));
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
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
        [
            new SelectedFactionUpgrade("Blessed Spawning Of Chotec", 10),
            new SelectedFactionUpgrade("Blessed Spawning Of Sotek", 10),
        ];

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
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
        [
            new SelectedFactionUpgrade("Blessed Spawning Of Chotec", 10),
        ];

        // 20 × 10 (base) + 20 × 1 × 1 (spawning) = 220
        // The unit's own per-model rate applies — not the 10 pts a character would pay.
        Assert.Equal(220, config.TotalPointsCost);
    }

    [Fact]
    public void FactionUpgradeCategoryFor_Option_PrefersItsOwnCategory()
    {
        var option = new UnitOption { Type = "faction_upgrade", FactionUpgrade = "gifts_of_khaine" };
        Assert.Equal("gifts_of_khaine", UnitConfiguration.FactionUpgradeCategoryFor(option));
    }

    [Fact]
    public void FactionUpgradeCategoryFor_Option_FallsBackToBlessedSpawningType()
    {
        var option = new UnitOption { Type = "blessed_spawning", Points = 2, PerModel = true };
        Assert.Equal("blessed_spawnings", UnitConfiguration.FactionUpgradeCategoryFor(option));
    }

    [Fact]
    public void FactionUpgradeCost_NoRateOnOption_ChargesEachPicksOwnPoints()
    {
        // Characters take upgrades through a shared allowance, so the option carries no rate
        // and each pick costs what the army book lists against it — 10 + 5 = 15.
        var unit = TestHelpers.MakeUnit(points: 100, options: new List<UnitOption>
        {
            new() { Type = "faction_upgrade", FactionUpgrade = "blessed_spawnings" },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 1 };
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
        [
            new SelectedFactionUpgrade("Blessed Spawning Of Chotec", 10),
            new SelectedFactionUpgrade("Blessed Spawning Of Itzl", 5),
        ];

        Assert.Equal(15, config.FactionUpgradeCost);
    }

    [Fact]
    public void FactionUpgradeCost_NoRateOnOption_DoesNotScaleWithModelCount()
    {
        var unit = TestHelpers.MakeUnit(points: 100, options: new List<UnitOption>
        {
            new() { Type = "faction_upgrade", FactionUpgrade = "blessed_spawnings" },
        });
        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
        [
            new SelectedFactionUpgrade("Blessed Spawning Of Chotec", 10),
        ];

        Assert.Equal(10, config.FactionUpgradeCost);
    }

    // ─── Data consistency ────────────────────────────────────────

    [Theory]
    [InlineData(FactionKey.Lizardmen, "Saurus Scar-veteran", "blessed_spawnings")]
    [InlineData(FactionKey.Lizardmen, "Saurus Oldblood", "blessed_spawnings")]
    [InlineData(FactionKey.Lizardmen, "Skink Priest", "blessed_spawnings")]
    [InlineData(FactionKey.Lizardmen, "Slann Mage-priest", "disciplines")]
    [InlineData(FactionKey.DarkElves, "Death Hag", "gifts_of_khaine")]
    [InlineData(FactionKey.DarkElves, "Khainite Assassin", "gifts_of_khaine")]
    public async Task Characters_SharingAnAllowance_OfferTheirUpgradeFamily(
        FactionKey faction, string unitName, string expectedCategory)
    {
        var units = await TestHelpers.Repo.GetUnitsAsync(faction);
        var unit = units.Single(u => u.Name == unitName);

        var categories = unit.Options
            .Select(UnitConfiguration.FactionUpgradeCategoryFor)
            .Where(c => c is not null)
            .ToList();

        Assert.Contains(expectedCategory, categories);
    }

    [Theory]
    [InlineData(FactionKey.Lizardmen)]
    [InlineData(FactionKey.DarkElves)]
    public async Task EveryOfferedUpgradeCategory_HasEntriesInFactionExtras(FactionKey faction)
    {
        var units = await TestHelpers.Repo.GetUnitsAsync(faction);
        var extras = await TestHelpers.Repo.GetFactionExtrasAsync(faction);

        var offered = units
            .SelectMany(u => u.Options)
            .Select(UnitConfiguration.FactionUpgradeCategoryFor)
            .Where(c => c is not null)
            .Distinct();

        foreach (var category in offered)
        {
            Assert.True(extras.FactionUpgrades.TryGetValue(category!, out var entries),
                $"{faction} offers '{category}' but its extras have no such category");
            Assert.NotEmpty(entries);
        }
    }

    [Theory]
    [InlineData(FactionKey.Lizardmen, "blessed_spawnings")]
    [InlineData(FactionKey.Lizardmen, "disciplines")]
    [InlineData(FactionKey.DarkElves, "gifts_of_khaine")]
    public async Task UpgradeEntries_AreRealUpgrades_NotSectionHeaders(
        FactionKey faction, string category)
    {
        var extras = await TestHelpers.Repo.GetFactionExtrasAsync(faction);
        var entries = extras.FactionUpgrades[category];

        // A section header parsed as an entry carries prose where its cost should be.
        foreach (var entry in entries)
        {
            Assert.True(double.TryParse(entry.Points, out var points),
                $"'{entry.Name}' has non-numeric points '{entry.Points}' — likely a section header");
            Assert.True(points > 0, $"'{entry.Name}' costs nothing");
        }
    }
}
