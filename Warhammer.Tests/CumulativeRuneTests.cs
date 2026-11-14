using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// Dwarf cumulative runes, priced "15/25/50": each figure is the price of one more copy, and the
/// figure count is the most copies one item may carry (Rules of the Runes, rule 4).
/// </summary>
public class CumulativeRuneTests
{
    private const string RunicItems = "runic_items";

    // ─── WAR: runes as the runic_items faction upgrade ───────────

    private static async Task<UpgradeEntry> WarRune(string name)
    {
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(FactionKey.Dwarfs);
        return extras.FactionUpgrades[RunicItems].Single(e => e.Name == name);
    }

    private static async Task<UnitConfiguration> WarThane()
    {
        var units = await TestHelpers.RepoFor(GameSystems.War).GetUnitsAsync(FactionKey.Dwarfs);
        return new UnitConfiguration(units.Single(u => u.Name == "Thane"));
    }

    [Fact]
    public async Task War_EveryRunicItem_IsRecognised()
    {
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(FactionKey.Dwarfs);
        var runes = extras.FactionUpgrades[RunicItems];

        Assert.NotEmpty(runes);
        Assert.All(runes, r => Assert.True(UpgradePrice.IsRecognised(r.Points), $"'{r.Name}' has unparsed points '{r.Points}'"));
        Assert.Contains(runes, r => UpgradePrice.IsCumulative(r.Points));
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 40)]
    [InlineData(3, 90)]
    public async Task War_Thane_RuneOfFury_CostsTheTotalForItsCopies(int copies, double expected)
    {
        var thane = await WarThane();
        var fury = await WarRune("RUNE OF FURY");

        thane.SetFactionUpgradeCopies(RunicItems, fury.Name, copies, fury.Points);

        var pick = Assert.Single(thane.Loadout.SelectedFactionUpgrades[RunicItems]);
        Assert.Equal(copies, pick.Copies);
        Assert.Equal(expected, thane.FactionUpgradeCost);
    }

    [Fact]
    public async Task War_Thane_ChangingCopies_ReplacesThePick_AndZeroRemovesIt()
    {
        var thane = await WarThane();
        var fury = await WarRune("RUNE OF FURY");

        thane.SetFactionUpgradeCopies(RunicItems, fury.Name, 3, fury.Points);
        thane.SetFactionUpgradeCopies(RunicItems, fury.Name, 2, fury.Points);
        Assert.Equal(2, Assert.Single(thane.Loadout.SelectedFactionUpgrades[RunicItems]).Copies);
        Assert.Equal(40, thane.FactionUpgradeCost);

        thane.SetFactionUpgradeCopies(RunicItems, fury.Name, 0, fury.Points);
        Assert.Empty(thane.Loadout.SelectedFactionUpgrades[RunicItems]);
        Assert.Equal(0, thane.FactionUpgradeCost);
    }

    [Fact]
    public async Task War_Thane_MoreCopiesThanFigures_TakesTheMost()
    {
        var thane = await WarThane();
        var might = await WarRune("RUNE OF MIGHT");

        thane.SetFactionUpgradeCopies(RunicItems, might.Name, 3, might.Points);

        Assert.Equal(2, Assert.Single(thane.Loadout.SelectedFactionUpgrades[RunicItems]).Copies);
        Assert.Equal(60, thane.FactionUpgradeCost);
    }

    [Fact]
    public async Task War_MasterRune_HasOneFigure_AndAllowsOneCopy()
    {
        var thane = await WarThane();
        var smiting = await WarRune("MASTER RUNE OF SMITING");
        Assert.Equal(1, UpgradePrice.MaxCopies(smiting.Points));

        thane.SetFactionUpgradeCopies(RunicItems, smiting.Name, 2, smiting.Points);

        var pick = Assert.Single(thane.Loadout.SelectedFactionUpgrades[RunicItems]);
        Assert.Equal(1, pick.Copies);
        Assert.Equal(35, thane.FactionUpgradeCost);
    }

    [Fact]
    public async Task War_RunesOfDifferentNames_AddUp()
    {
        var thane = await WarThane();
        var fury = await WarRune("RUNE OF FURY");
        var smiting = await WarRune("MASTER RUNE OF SMITING");

        thane.SetFactionUpgradeCopies(RunicItems, fury.Name, 2, fury.Points);
        thane.SetFactionUpgradeCopies(RunicItems, smiting.Name, 1, smiting.Points);

        Assert.Equal(2, thane.Loadout.SelectedFactionUpgrades[RunicItems].Count);
        Assert.Equal(40 + 35, thane.FactionUpgradeCost);
    }

    // ─── wap-v2: runes as ordinary magic items ───────────────────

    private static async Task<List<MagicItemEntry>> WapV2DwarfItems() =>
        (await TestHelpers.RepoFor(GameSystems.WapV2).GetFactionExtrasAsync(FactionKey.Dwarfs)).MagicItems;

    /// <summary>Add a copy the way the selector does, or return false when it is refused.</summary>
    private static bool TryAdd(List<SelectedMagicItem> held, MagicItemEntry entry)
    {
        if (MagicItemPricing.NextCopyCost(held, entry) is not { } cost) return false;
        held.Add(new SelectedMagicItem { Name = entry.Name, Category = entry.Category, PointsCost = cost });
        return true;
    }

    [Fact]
    public async Task WapV2_EveryDwarfMagicItem_IsRecognised()
    {
        var items = await WapV2DwarfItems();

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.True(UpgradePrice.IsRecognised(i.Points), $"'{i.Name}' has unparsed points '{i.Points}'"));
    }

    [Fact]
    public async Task WapV2_RuneOfFury_EachCopyCostsTheNextFigure_AndAFourthIsRefused()
    {
        var fury = (await WapV2DwarfItems()).Single(i => i.Name == "Rune Of Fury");
        var held = new List<SelectedMagicItem>();

        Assert.True(TryAdd(held, fury));
        Assert.True(TryAdd(held, fury));
        Assert.True(TryAdd(held, fury));

        Assert.Equal([15d, 25d, 50d], held.Select(i => i.PointsCost));
        Assert.Null(MagicItemPricing.NextCopyCost(held, fury));
        Assert.False(TryAdd(held, fury));
        Assert.Equal(3, held.Count);
    }

    [Fact]
    public async Task WapV2_RemovingACopy_RepricesTheRestInListOrder()
    {
        var items = await WapV2DwarfItems();
        var fury = items.Single(i => i.Name == "Rune Of Fury");
        var other = items.First(i => !UpgradePrice.IsCumulative(i.Points));
        var held = new List<SelectedMagicItem>();
        TryAdd(held, fury);
        TryAdd(held, other);
        TryAdd(held, fury);
        TryAdd(held, fury);

        held.RemoveAt(0);
        MagicItemPricing.RepriceCopies(held, fury);

        Assert.Equal([15d, 25d], held.Where(i => i.Name == fury.Name).Select(i => i.PointsCost));
        Assert.Equal(MagicItemPricing.FirstCopyCost(other.Points), held.Single(i => i.Name == other.Name).PointsCost);
        Assert.Equal(50d, MagicItemPricing.NextCopyCost(held, fury));
    }

    [Fact]
    public async Task WapV2_NonCumulativeItem_CostsItsOnePrice_HoweverManyAreHeld()
    {
        var item = (await WapV2DwarfItems()).First(i => !UpgradePrice.IsCumulative(i.Points));
        var held = new List<SelectedMagicItem>();

        TryAdd(held, item);

        Assert.Equal(double.Parse(item.Points, System.Globalization.CultureInfo.InvariantCulture),
            MagicItemPricing.NextCopyCost(held, item));
    }
}
