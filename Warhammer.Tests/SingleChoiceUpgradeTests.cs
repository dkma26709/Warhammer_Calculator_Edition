using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class SingleChoiceUpgradeTests
{
    [Theory]
    [InlineData("May be upgraded to a Knightly Order no points limit")]
    [InlineData("A Baron may take one Virtue and/or Magic Items up to a total of 100 points")]
    [InlineData("A Noble may take one Elven Honour and Magic Items up to a total of 50 points")]
    [InlineData("May take one Clan Mon and/or Magic Items up to a total of 50 points")]
    [InlineData("May take a Clan Mon +15 points")]
    [InlineData("May take one Saga and/or Magic Items up to a total of 50 points")]
    [InlineData("May take a Blessed Spawning +2 points/model")]
    public void IsSingleChoice_OneOfAFamily_IsTrue(string raw)
    {
        Assert.True(UnitConfiguration.IsSingleChoice(new UnitOption { Type = "faction_upgrade", Raw = raw }));
    }

    [Theory]
    [InlineData("A Mercenary General may take Quirks of Character and/or Magic Items up to a total of 100 points")]
    [InlineData("May take Blessed Spawnings and/or Magic Items up to a total of 50 points")]
    [InlineData("May take Quirks of Character no points limit")]
    [InlineData("A King may take Magic Items and/or Runic Items up to a total of 100 points")]
    public void IsSingleChoice_AnyNumberOfAFamily_IsFalse(string raw)
    {
        Assert.False(UnitConfiguration.IsSingleChoice(new UnitOption { Type = "faction_upgrade", Raw = raw }));
    }

    [Fact]
    public void IsSingleChoice_NoRawText_IsFalse()
    {
        Assert.False(UnitConfiguration.IsSingleChoice(new UnitOption { Type = "faction_upgrade" }));
    }

    [Fact]
    public async Task War_SingleChoiceCategories_AreExactlyTheOneOfFamilies()
    {
        var repo = TestHelpers.RepoFor(GameSystems.War);
        var found = new HashSet<string>();

        foreach (var faction in repo.AllFactions)
            foreach (var unit in await repo.GetUnitsAsync(faction))
                foreach (var option in unit.Options)
                    if (UnitConfiguration.FactionUpgradeCategoryFor(option) is { } category
                        && UnitConfiguration.IsSingleChoice(option))
                        found.Add(category);

        string[] expected =
        [
            "knightly_orders", "virtues_of_the_chivalric_knight", "elven_honours",
            "clan_mon", "sagas", "blessed_spawnings",
        ];
        Assert.Equal(expected.Order(), found.Order());
    }

    // ─── Toggling picks on WAR units ─────────────────────────────

    private static async Task<UnitConfiguration> WarUnit(FactionKey faction, string unitName)
    {
        var units = await TestHelpers.RepoFor(GameSystems.War).GetUnitsAsync(faction);
        return new UnitConfiguration(units.Single(u => u.Name == unitName));
    }

    /// <summary>A pick priced for <paramref name="cfg"/>, as the Builder makes it.</summary>
    private static async Task<SelectedFactionUpgrade> WarPick(
        UnitConfiguration cfg, FactionKey faction, string category, string name)
    {
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(faction);
        var entry = extras.FactionUpgrades[category].Single(e => e.Name == name);
        var price = cfg.ResolveUpgradePrice(entry.Points);
        Assert.NotNull(price);
        return new SelectedFactionUpgrade(entry.Name, price.Value.Points, price.Value.PerModel);
    }

    [Fact]
    public async Task KnightlyOrder_PickingAnother_ReplacesTheFirst_AndUntickingLeavesNone()
    {
        var knights = await WarUnit(FactionKey.TheEmpire, "KNIGHTS");
        var panther = await WarPick(knights, FactionKey.TheEmpire, "knightly_orders", "KNIGHTS PANTHER");
        var whiteWolf = await WarPick(knights, FactionKey.TheEmpire, "knightly_orders", "KNIGHTS OF THE WHITE WOLF");

        Assert.True(knights.IsSingleChoiceCategory("knightly_orders"));
        knights.ToggleFactionUpgrade("knightly_orders", panther, true);
        knights.ToggleFactionUpgrade("knightly_orders", whiteWolf, true);

        var pick = Assert.Single(knights.Loadout.SelectedFactionUpgrades["knightly_orders"]);
        Assert.Equal("KNIGHTS OF THE WHITE WOLF", pick.Name);
        Assert.Equal(1 * knights.ModelCount, knights.FactionUpgradeCost);

        knights.ToggleFactionUpgrade("knightly_orders", whiteWolf, false);

        Assert.Empty(knights.Loadout.SelectedFactionUpgrades["knightly_orders"]);
        Assert.Equal(0, knights.FactionUpgradeCost);
    }

    [Fact]
    public async Task QuirksOfCharacter_StayMultiPick()
    {
        var general = await WarUnit(FactionKey.DogsOfWar, "Mercenary General");
        var grizzled = await WarPick(general, FactionKey.DogsOfWar, "quirks_of_character", "GRIZZLED WARRIOR");
        var charismatic = await WarPick(general, FactionKey.DogsOfWar, "quirks_of_character", "CHARISMATIC LEADER");

        Assert.False(general.IsSingleChoiceCategory("quirks_of_character"));
        general.ToggleFactionUpgrade("quirks_of_character", grizzled, true);
        general.ToggleFactionUpgrade("quirks_of_character", charismatic, true);

        Assert.Equal(
            ["GRIZZLED WARRIOR", "CHARISMATIC LEADER"],
            general.Loadout.SelectedFactionUpgrades["quirks_of_character"].Select(p => p.Name));
    }
}
