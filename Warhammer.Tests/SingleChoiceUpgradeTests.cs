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

        Assert.Equal(1, knights.MaxPicks("knightly_orders"));
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

        Assert.Null(general.MaxPicks("quirks_of_character"));
        general.ToggleFactionUpgrade("quirks_of_character", grizzled, true);
        general.ToggleFactionUpgrade("quirks_of_character", charismatic, true);

        Assert.Equal(
            ["GRIZZLED WARRIOR", "CHARISMATIC LEADER"],
            general.Loadout.SelectedFactionUpgrades["quirks_of_character"].Select(p => p.Name));
    }

    // ─── Blessed Spawnings: the chapter intro's limits ───────────

    private const string Spawnings = "blessed_spawnings";

    private static IEnumerable<string> PickNames(UnitConfiguration cfg, string category) =>
        cfg.Loadout.SelectedFactionUpgrades[category].Select(p => p.Name);

    /// <summary>"Lords may take up to 3 Blessed Spawnings. Heroes, Core and Special units may take up to 2."</summary>
    private static readonly Dictionary<string, int> LizardmenSpawningLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Oldblood"] = 3, ["Saurus Oldblood"] = 3, ["High Chief"] = 3, ["Skink High Chief"] = 3,
        ["High Priest"] = 3, ["Skink High Priest"] = 3,
        ["Scar-Veteran"] = 2, ["Saurus Scar-veteran"] = 2, ["Chief"] = 2, ["Skink Chief"] = 2,
        ["Priest"] = 2, ["Skink Priest"] = 2, ["Kroxigor Ancient"] = 2, ["Chameleon Stalker"] = 2,
        ["Saurus Warriors"] = 2, ["Skink Cohort"] = 2, ["Skink Skirmishers"] = 2, ["Raptadon Riders"] = 2,
        ["Temple Guard"] = 2, ["Cold One Riders"] = 2, ["Kroxigors"] = 2, ["Chameleon Skinks"] = 2,
        ["Horned One Riders"] = 2, ["Terradon Riders"] = 2, ["Ripperdactyl Riders"] = 2,
        // Rare is not in the intro, so the unit line's "a Blessed Spawning" stands.
        ["Aggradon Riders"] = 1,
    };

    [Theory]
    [InlineData(GameSystems.War)]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task Lizardmen_BlessedSpawningLimits_FollowTheChapterIntro(string system)
    {
        var units = await TestHelpers.RepoFor(system).GetUnitsAsync(FactionKey.Lizardmen);
        var offering = units
            .Where(u => u.Options.Any(o => UnitConfiguration.FactionUpgradeCategoryFor(o) == Spawnings))
            .Select(u => (u.Name, Limit: new UnitConfiguration(u).MaxPicks(Spawnings)))
            .ToList();

        var wrong = offering
            .Where(u => !LizardmenSpawningLimits.TryGetValue(u.Name, out var expected) || expected != u.Limit)
            .Select(u => $"{u.Name}: {u.Limit?.ToString() ?? "unlimited"}");
        Assert.Empty(wrong);
        Assert.Equal(20, offering.Count);
        Assert.Equal([1, 2, 3], offering.Select(u => u.Limit!.Value).Distinct().Order());
    }

    [Fact]
    public async Task SaurusWarriors_KeepTwoSpawnings_AndRefuseAThird()
    {
        var warriors = await WarUnit(FactionKey.Lizardmen, "SAURUS WARRIORS");
        var chotec = await WarPick(warriors, FactionKey.Lizardmen, Spawnings, "BLESSED SPAWNING OF CHOTEC");
        var tepok = await WarPick(warriors, FactionKey.Lizardmen, Spawnings, "BLESSED SPAWNING OF TEPOK");
        var sotek = await WarPick(warriors, FactionKey.Lizardmen, Spawnings, "BLESSED SPAWNING OF SOTEK");

        Assert.True(warriors.ToggleFactionUpgrade(Spawnings, chotec, true));
        Assert.True(warriors.ToggleFactionUpgrade(Spawnings, tepok, true));
        Assert.False(warriors.ToggleFactionUpgrade(Spawnings, sotek, true));

        Assert.Equal([chotec.Name, tepok.Name], PickNames(warriors, Spawnings));
        var rate = warriors.ResolvedUnit.Options
            .Single(o => UnitConfiguration.FactionUpgradeCategoryFor(o) == Spawnings).Points!.Value;
        Assert.Equal(rate * 2 * warriors.ModelCount, warriors.FactionUpgradeCost);

        Assert.True(warriors.ToggleFactionUpgrade(Spawnings, chotec, false));
        Assert.True(warriors.ToggleFactionUpgrade(Spawnings, sotek, true));

        Assert.Equal([tepok.Name, sotek.Name], PickNames(warriors, Spawnings));
    }

    [Fact]
    public async Task AggradonRiders_ASecondSpawning_ReplacesTheFirst()
    {
        var riders = await WarUnit(FactionKey.Lizardmen, "AGGRADON RIDERS");
        var chotec = await WarPick(riders, FactionKey.Lizardmen, Spawnings, "BLESSED SPAWNING OF CHOTEC");
        var tepok = await WarPick(riders, FactionKey.Lizardmen, Spawnings, "BLESSED SPAWNING OF TEPOK");

        Assert.True(riders.ToggleFactionUpgrade(Spawnings, chotec, true));
        Assert.True(riders.ToggleFactionUpgrade(Spawnings, tepok, true));

        Assert.Equal([tepok.Name], PickNames(riders, Spawnings));
    }

    [Theory]
    [InlineData("Oldblood", 3)]
    [InlineData("Scar-Veteran", 2)]
    public async Task Character_KeepsSpawningsUpToItsLimit_AndRefusesOneMore(string unitName, int limit)
    {
        var character = await WarUnit(FactionKey.Lizardmen, unitName);
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(FactionKey.Lizardmen);
        var picks = new List<SelectedFactionUpgrade>();
        foreach (var entry in extras.FactionUpgrades[Spawnings].Take(limit + 1))
            picks.Add(await WarPick(character, FactionKey.Lizardmen, Spawnings, entry.Name));

        foreach (var pick in picks.Take(limit))
            Assert.True(character.ToggleFactionUpgrade(Spawnings, pick, true));
        Assert.False(character.ToggleFactionUpgrade(Spawnings, picks[limit], true));

        Assert.Equal(picks.Take(limit).Select(p => p.Name), PickNames(character, Spawnings));
    }

    // ─── PicksAfterTick ──────────────────────────────────────────

    /// <summary>A Core unit offering Blessed Spawnings, so two may be taken, holding <paramref name="taken"/>.</summary>
    private static UnitConfiguration CoreSpawningUnit(params SelectedFactionUpgrade[] taken)
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", options:
            [new UnitOption { Type = "faction_upgrade", FactionUpgrade = Spawnings, Raw = "May take a Blessed Spawning +1 point/model" }]);
        var cfg = new UnitConfiguration(unit);
        cfg.Loadout.SelectedFactionUpgrades[Spawnings] = [.. taken];
        return cfg;
    }

    [Fact]
    public void PicksAfterTick_ReTickingATakenName_ReplacesItInPlace_EvenWhenFull()
    {
        var cfg = CoreSpawningUnit(new("Chotec", 10), new("Tepok", 10));
        var repriced = new SelectedFactionUpgrade("Chotec", 15);

        var after = cfg.PicksAfterTick(Spawnings, repriced);

        Assert.NotNull(after);
        Assert.Equal([repriced, new SelectedFactionUpgrade("Tepok", 10)], after);
    }

    [Fact]
    public void PicksAfterTick_NeverChangesTheLoadout()
    {
        SelectedFactionUpgrade chotec = new("Chotec", 10), tepok = new("Tepok", 10), sotek = new("Sotek", 10);
        var open = CoreSpawningUnit(chotec);
        var full = CoreSpawningUnit(chotec, tepok);

        Assert.Equal([chotec, tepok], open.PicksAfterTick(Spawnings, tepok));
        Assert.Null(full.PicksAfterTick(Spawnings, sotek));
        Assert.NotNull(full.PicksAfterTick(Spawnings, chotec with { PointsCost = 15 }));

        Assert.Equal([chotec], open.Loadout.SelectedFactionUpgrades[Spawnings]);
        Assert.Equal([chotec, tepok], full.Loadout.SelectedFactionUpgrades[Spawnings]);
    }
}
