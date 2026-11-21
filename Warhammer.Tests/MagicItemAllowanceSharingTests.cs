using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// "May take Quirks of Character and/or Magic Items up to a total of 100 points" is one budget:
/// the upgrades the line offers spend the magic item allowance alongside the items.
/// </summary>
public class MagicItemAllowanceSharingTests
{
    private const string Quirks = "quirks_of_character";

    [Theory]
    [InlineData("A Mercenary General may take Quirks of Character and/or Magic Items up to a total of 100 points")]
    [InlineData("A Noble may take one Elven Honour and Magic Items up to a total of 50 points")]
    [InlineData("May take Blessed Spawnings and/or Magic Items up to a total of 50 points")]
    public void SharesMagicItemAllowance_LinesNamingMagicItems(string raw)
    {
        Assert.True(UnitConfiguration.SharesMagicItemAllowance(new UnitOption { Type = "faction_upgrade", Raw = raw }));
    }

    [Theory]
    [InlineData("May take Quirks of Character no points limit")]
    [InlineData("May be upgraded to a Knightly Order no points limit")]
    [InlineData("May take a Blessed Spawning +2 points/model")]
    public void SharesMagicItemAllowance_NotLinesWithoutMagicItems(string raw)
    {
        Assert.False(UnitConfiguration.SharesMagicItemAllowance(new UnitOption { Type = "faction_upgrade", Raw = raw }));
    }

    /// <summary>A WAR unit with <paramref name="upgrades"/> picked the way the Builder does: priced for it.</summary>
    private static async Task<UnitConfiguration> WarUnitWith(
        FactionKey faction, string unitName, string category, params string[] upgrades)
    {
        var repo = TestHelpers.RepoFor(GameSystems.War);
        var cfg = new UnitConfiguration((await repo.GetUnitsAsync(faction)).Single(u => u.Name == unitName));

        foreach (var name in upgrades)
            cfg.ToggleFactionUpgrade(category, await PickFor(cfg, category, name), true);
        return cfg;
    }

    /// <summary>The pick the Builder makes for the upgrade <paramref name="name"/>: priced for <paramref name="unit"/>.</summary>
    private static async Task<SelectedFactionUpgrade> PickFor(UnitConfiguration unit, string category, string name)
    {
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(unit.ResolvedUnit.Faction);
        var entry = extras.FactionUpgrades[category].Single(e => e.Name == name);
        var price = unit.ResolveUpgradePrice(entry.Points);
        Assert.NotNull(price);
        return new SelectedFactionUpgrade(entry.Name, price.Value.Points, price.Value.PerModel);
    }

    private static Task<UnitConfiguration> General(params string[] quirks) =>
        WarUnitWith(FactionKey.DogsOfWar, "Mercenary General", Quirks, quirks);

    private const string Virtues = "virtues_of_the_chivalric_knight";

    private static Task<UnitConfiguration> Paladin(params string[] virtues) =>
        WarUnitWith(FactionKey.Bretonnia, "Paladin", Virtues, virtues);

    private static void GiveItem(UnitConfiguration unit, double points) =>
        unit.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Name = "Sword of Might",
            Category = MagicItemCategories.MagicWeapon,
            PointsCost = points,
        });

    private static List<ValidationIssue> AllowanceIssues(UnitConfiguration unit) =>
        new ArmyListValidator()
            .Validate(new ArmyList { Faction = unit.ResolvedUnit.Faction, PointsTarget = 2000, Units = [unit] },
                TestCompositionRules.WapV2)
            .Where(i => i.Code is "MAGIC_ITEMS_OVER_BUDGET" or "MAGIC_ITEMS_NOT_ALLOWED")
            .ToList();

    // ─── Cost ────────────────────────────────────────────────────

    [Fact]
    public async Task MercenaryGeneral_QuirksSpendTheAllowance()
    {
        var general = await General("GRIZZLED WARRIOR", "CHARISMATIC LEADER");
        Assert.Equal(100, general.ResolvedUnit.MagicItemAllowance);
        Assert.Equal(50, general.AllowanceUpgradeCost);
        Assert.Equal(50, general.FactionUpgradeCost);
        Assert.Equal(50, general.MagicItemAllowanceSpent);

        GiveItem(general, 30);
        Assert.Equal(80, general.MagicItemAllowanceSpent);
        Assert.Equal(50, general.FactionUpgradeCost);
    }

    [Fact]
    public async Task Knights_KnightlyOrder_DoesNotSpendAnAllowance()
    {
        var knights = await WarUnitWith(FactionKey.TheEmpire, "KNIGHTS", "knightly_orders", "KNIGHTS PANTHER");
        Assert.True(knights.FactionUpgradeCost > 0);
        Assert.Equal(0, knights.AllowanceUpgradeCost);
        Assert.Empty(knights.AllowanceUpgradeCategories);
    }

    // ─── Validation ──────────────────────────────────────────────

    [Fact]
    public async Task QuirksPlusItems_OverTheAllowance_FlagsOverBudget_NamingTheQuirks()
    {
        var general = await General("GRIZZLED WARRIOR", "CHARISMATIC LEADER");
        GiveItem(general, 60);

        var issue = Assert.Single(AllowanceIssues(general));

        Assert.Equal("MAGIC_ITEMS_OVER_BUDGET", issue.Code);
        Assert.Equal(
            "Mercenary General spends 110 on magic items and quirks of character, over its 100 allowance",
            issue.Message);
    }

    [Fact]
    public async Task QuirksPlusItems_WithinTheAllowance_NoIssue()
    {
        var general = await General("GRIZZLED WARRIOR", "CHARISMATIC LEADER");
        GiveItem(general, 50);

        Assert.Empty(AllowanceIssues(general));
    }

    [Fact]
    public async Task QuirksAlone_OverTheAllowance_FlagsOverBudget()
    {
        // 25 + 25 + 20 + 20 + 15 = 105, with no magic item at all.
        var general = await General("GRIZZLED WARRIOR", "CHARISMATIC LEADER", "GREEDY", "EAGLE EYE", "BEASTSLAYER");

        var issue = Assert.Single(AllowanceIssues(general));

        Assert.Equal("MAGIC_ITEMS_OVER_BUDGET", issue.Code);
        Assert.Equal("Mercenary General spends 105 on quirks of character, over its 100 allowance", issue.Message);
    }

    [Fact]
    public async Task ItemsAlone_OverTheAllowance_KeepTheItemOnlyWording()
    {
        var general = await General();
        GiveItem(general, 110);

        var issue = Assert.Single(AllowanceIssues(general));

        Assert.Equal("Mercenary General spends 110 on magic items, over its 100 allowance", issue.Message);
    }

    [Fact]
    public async Task RankAndFile_WithQuirks_RaiseNoAllowanceIssue()
    {
        var pikemen = await WarUnitWith(FactionKey.DogsOfWar, "PIKEMEN", Quirks, "FEARLESS", "DRILLED");
        Assert.True(pikemen.FactionUpgradeCost > 0);
        Assert.Equal(0, pikemen.AllowanceUpgradeCost);

        Assert.Empty(AllowanceIssues(pikemen));
    }

    // ─── Picker: what still fits ─────────────────────────────────

    [Fact]
    public async Task Remaining_IsTheAllowanceLessItemsAndQuirks()
    {
        var general = await General();
        GiveItem(general, 80);

        Assert.Equal(20, general.MagicItemAllowanceRemaining);
    }

    [Fact]
    public async Task Remaining_IsNull_WithoutAnAllowance()
    {
        var knights = await WarUnitWith(FactionKey.TheEmpire, "KNIGHTS", "knightly_orders");

        Assert.Null(knights.ResolvedUnit.MagicItemAllowance);
        Assert.Null(knights.MagicItemAllowanceRemaining);
    }

    [Fact]
    public async Task Quirk_FitsOnlyWhatIsLeft()
    {
        var general = await General();
        GiveItem(general, 80);

        Assert.False(general.UpgradeFitsAllowance(Quirks, await PickFor(general, Quirks, "GRIZZLED WARRIOR")));
        Assert.True(general.UpgradeFitsAllowance(Quirks, await PickFor(general, Quirks, "BERSERKER")));
    }

    [Fact]
    public async Task Quirk_AlreadyPicked_IsNotCountedTwice()
    {
        var general = await General("BERSERKER");
        GiveItem(general, 80);
        Assert.Equal(10, general.MagicItemAllowanceRemaining);

        Assert.True(general.UpgradeFitsAllowance(Quirks, await PickFor(general, Quirks, "BERSERKER")));
    }

    [Fact]
    public async Task Virtue_ReplacesThePickedOne_SoOnlyTheNewPriceCounts()
    {
        var paladin = await Paladin("VIRTUE OF HEROISM");
        GiveItem(paladin, 15);
        Assert.Equal(0, paladin.MagicItemAllowanceRemaining);

        // 15 + 30 fits the 50, because the Ideal replaces the Heroism.
        Assert.True(paladin.UpgradeFitsAllowance(Virtues, await PickFor(paladin, Virtues, "VIRTUE OF THE IDEAL")));
    }

    [Fact]
    public async Task Virtue_OverTheAllowance_OnlyACheaperOneFits()
    {
        var paladin = await Paladin("VIRTUE OF HEROISM");
        GiveItem(paladin, 25);

        Assert.False(paladin.UpgradeFitsAllowance(Virtues, await PickFor(paladin, Virtues, "VIRTUE OF THE IDEAL")));
        Assert.True(paladin.UpgradeFitsAllowance(Virtues, await PickFor(paladin, Virtues, "VIRTUE OF DEVOTION")));
    }

    [Fact]
    public async Task ScarVeteran_SpawningsSpendTheAllowance_AndStopAtTwo()
    {
        const string spawnings = "blessed_spawnings";
        var veteran = await WarUnitWith(FactionKey.Lizardmen, "Scar-Veteran", spawnings,
            "BLESSED SPAWNING OF CHOTEC", "BLESSED SPAWNING OF TEPOK");
        Assert.Equal(30, veteran.MagicItemAllowanceRemaining);
        var third = await PickFor(veteran, spawnings, "BLESSED SPAWNING OF ITZL");

        // The limit refuses the third, not the allowance: it would fit the 30 left, and the picks stay as they are.
        Assert.Null(veteran.PicksAfterTick(spawnings, third));
        Assert.True(veteran.UpgradeFitsAllowance(spawnings, third));
        Assert.False(veteran.ToggleFactionUpgrade(spawnings, third, true));
        Assert.Equal(30, veteran.MagicItemAllowanceRemaining);
    }

    [Fact]
    public async Task Knights_AnyKnightlyOrderFits()
    {
        var knights = await WarUnitWith(FactionKey.TheEmpire, "KNIGHTS", "knightly_orders", "KNIGHTS PANTHER");

        Assert.True(knights.UpgradeFitsAllowance("knightly_orders",
            await PickFor(knights, "knightly_orders", "KNIGHTS OF THE TWIN-TAILED ORB")));
    }

    [Fact]
    public async Task GrandMaster_KnightlyOrderFits_EvenWithTheAllowanceSpent()
    {
        // The Grand Master has an allowance, but his Knightly Order line does not draw on it.
        var master = await WarUnitWith(FactionKey.TheEmpire, "Grand Master", "knightly_orders");
        Assert.NotNull(master.ResolvedUnit.MagicItemAllowance);
        GiveItem(master, master.ResolvedUnit.MagicItemAllowance!.Value + 10);

        Assert.True(master.UpgradeFitsAllowance("knightly_orders",
            await PickFor(master, "knightly_orders", "SONS OF MANANN")));
    }
}
