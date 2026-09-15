using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class FactionUpgradeTests
{
    [Fact]
    public void SelectedFactionUpgrades_DefaultsToEmpty()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Empty(config.Loadout.SelectedFactionUpgrades);
    }

    [Fact]
    public void FactionUpgradeCost_PerModelRate_ScalesWithModelCountAndSelectionCount()
    {
        // 20 models × 1 pt/model × 2 spawnings selected = 40 pts
        var unit = TestHelpers.MakeUnit(points: 10, options: [TestHelpers.FactionUpgrade("blessed-spawnings", rate: 1, perModel: true)]);
        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Loadout.SelectedFactionUpgrades["blessed-spawnings"] =
        [
            new SelectedFactionUpgrade("blessed-spawning-of-chotec", "Blessed Spawning Of Chotec", 10),
            new SelectedFactionUpgrade("blessed-spawning-of-sotek", "Blessed Spawning Of Sotek", 10),
        ];

        Assert.Equal(40, config.FactionUpgradeCost);
    }

    [Fact]
    public void FactionUpgradeCost_ZeroWhenNoneSelected()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.FactionUpgrade("blessed-spawnings", rate: 1, perModel: true)]);
        var config = new UnitConfiguration(unit) { ModelCount = 20 };

        Assert.Equal(0, config.FactionUpgradeCost);
    }

    [Fact]
    public void TotalPointsCost_IncludesFactionUpgradeCost()
    {
        var unit = TestHelpers.MakeUnit(points: 10, options: [TestHelpers.FactionUpgrade("blessed-spawnings", rate: 1, perModel: true)]);
        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Loadout.SelectedFactionUpgrades["blessed-spawnings"] =
        [
            new SelectedFactionUpgrade("blessed-spawning-of-chotec", "Blessed Spawning Of Chotec", 10),
        ];

        // 20 × 10 (base) + 20 × 1 × 1 (spawning) = 220
        // The unit's own per-model rate applies — not the 10 pts a character would pay.
        Assert.Equal(220, config.TotalPointsCost);
    }

    [Fact]
    public void FactionUpgradeCost_NoRateOnTheGroup_ChargesEachPicksOwnPoints()
    {
        // Characters take upgrades priced individually, so the group carries no rate and each
        // pick costs what the army book lists against it — 10 + 5 = 15.
        var unit = TestHelpers.MakeUnit(points: 100, troopType: "Infantry (Character)",
            options: [TestHelpers.FactionUpgrade("knightly-orders")]);
        var config = new UnitConfiguration(unit) { ModelCount = 1 };
        config.Loadout.SelectedFactionUpgrades["knightly-orders"] =
        [
            new SelectedFactionUpgrade("a", "Order A", 10),
            new SelectedFactionUpgrade("b", "Order B", 5),
        ];

        Assert.Equal(15, config.FactionUpgradeCost);
    }

    [Fact]
    public void OfferedFamilies_ComeFromGroupsAndFromTheMagicItemAllowance()
    {
        var unit = TestHelpers.MakeUnit(troopType: "Infantry (Character)", options:
        [
            TestHelpers.FactionUpgrade("runic-items", pointsLimit: 50),
            TestHelpers.MagicItems(100, families: ["virtues"]),
        ]);
        var config = new UnitConfiguration(unit);

        var offered = config.OfferedFamilies.ToDictionary(f => f.Family);
        Assert.Equal(50, offered["runic-items"].PointsLimit);
        Assert.False(offered["runic-items"].ThroughMagicItems);
        Assert.True(offered["virtues"].ThroughMagicItems);
        Assert.Equal(100, offered["virtues"].PointsLimit);
    }

    // ─── Pricing the book's entries ─────────────────────────────────

    [Fact]
    public void FamilyOf_IsTheChaptersSlug()
    {
        var entry = new UpgradeEntry { Chapter = "KNIGHTLY ORDERS", Name = "REIKSGUARD KNIGHTS" };
        Assert.Equal("knightly-orders", FactionUpgrades.FamilyOf(entry));
    }

    [Fact]
    public void AFlatCost_IsTheSameForEveryone()
    {
        var cost = new UpgradeCost { Flat = 35 };
        Assert.Equal((35, false), FactionUpgrades.ResolveCost(cost, isCharacter: true, BaseTroopType.Infantry));
        Assert.Equal((35, false), FactionUpgrades.ResolveCost(cost, isCharacter: false, BaseTroopType.Cavalry));
    }

    [Fact]
    public void ATieredCost_IsReadByWhoTakesIt()
    {
        // The Empire's Knightly Orders: Characters free, Cavalry/Infantry 1 pt/model, Monstrous Cavalry 3 pts/model.
        var cost = new UpgradeCost
        {
            Tiers =
            [
                new UpgradeCostTier { Scope = "Characters", Text = "free" },
                new UpgradeCostTier { Scope = "Cavalry/Infantry", Text = "1 point per model" },
                new UpgradeCostTier { Scope = "Monstrous Cavalry", Text = "3 points per model" },
            ],
        };

        Assert.Equal((0, false), FactionUpgrades.ResolveCost(cost, isCharacter: true, BaseTroopType.Cavalry));
        Assert.Equal((1, true), FactionUpgrades.ResolveCost(cost, isCharacter: false, BaseTroopType.Cavalry));
        Assert.Equal((1, true), FactionUpgrades.ResolveCost(cost, isCharacter: false, BaseTroopType.Infantry));
        Assert.Equal((3, true), FactionUpgrades.ResolveCost(cost, isCharacter: false, BaseTroopType.MonstrousCavalry));
    }

    [Theory]
    [InlineData("free", 0, false)]
    [InlineData("25 points", 25, false)]
    [InlineData("3 points per model", 3, true)]
    [InlineData("0.5 pts/model", 0.5, true)]
    [InlineData(null, 0, false)]
    public void CostText_Parses(string? text, double points, bool perModel)
    {
        Assert.Equal((points, perModel), FactionUpgrades.ParseCostText(text));
    }

    // ─── Data consistency ────────────────────────────────────────

    [Theory]
    [InlineData("empire", "KNIGHTS", "knightly-orders")]
    [InlineData("lizardmen", "SAURUS WARRIORS", "blessed-spawnings")]
    [InlineData("dwarfs", "BOLT THROWER", "runic-items")]
    public async Task Entries_OfferTheirUpgradeFamily(string factionId, string unitName, string family)
    {
        var unit = await TestHelpers.FindUnitAsync(factionId, unitName);
        var config = new UnitConfiguration(unit);

        Assert.Contains(family, config.OfferedFamilies.Select(f => f.Family));
    }

    [Theory]
    [InlineData("empire")]
    [InlineData("lizardmen")]
    [InlineData("dwarfs")]
    [InlineData("bretonnia")]
    public async Task EveryOfferedFamily_HasEntriesInTheBook(string factionId)
    {
        var units = await TestHelpers.Repo.GetUnitsAsync(factionId);
        var faction = await TestHelpers.Repo.GetFactionAsync(factionId);

        var offered = units
            .SelectMany(u => new UnitConfiguration(u).OfferedFamilies)
            .Select(f => f.Family)
            .Distinct()
            .ToList();

        Assert.NotEmpty(offered);
        foreach (var family in offered)
            Assert.True(FactionUpgrades.InFamily(faction.Upgrades, family).Any(), $"{factionId}: '{family}' has no entries");
    }
}
