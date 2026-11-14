using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// "May take engineering runes up to 50 points" caps the picks taken through that option at its
/// own points limit, on an entry with no magic item allowance to spend.
/// </summary>
public class UpgradePointsLimitTests
{
    private const string Runes = "runic_items";

    /// <summary>A WAR Dwarf entry with <paramref name="runes"/> picked the way the Builder does: priced for it.</summary>
    private static async Task<UnitConfiguration> DwarfWith(string unitName, params string[] runes)
    {
        var repo = TestHelpers.RepoFor(GameSystems.War);
        var cfg = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Dwarfs)).Single(u => u.Name == unitName));

        foreach (var name in runes)
            cfg.ToggleFactionUpgrade(Runes, await PickFor(cfg, name), true);
        return cfg;
    }

    /// <summary>The pick the Builder makes for the rune <paramref name="name"/>: priced for <paramref name="unit"/>.</summary>
    private static async Task<SelectedFactionUpgrade> PickFor(UnitConfiguration unit, string name)
    {
        var extras = await TestHelpers.RepoFor(GameSystems.War).GetFactionExtrasAsync(FactionKey.Dwarfs);
        var entry = extras.FactionUpgrades[Runes].Single(e => e.Name == name);
        var price = unit.ResolveUpgradePrice(entry.Points);
        Assert.NotNull(price);
        return new SelectedFactionUpgrade(entry.Name, price.Value.Points, price.Value.PerModel);
    }

    private static Task<UnitConfiguration> Cannon(params string[] runes) => DwarfWith("CANNON", runes);

    private static List<ValidationIssue> LimitIssues(UnitConfiguration unit) =>
        new ArmyListValidator()
            .Validate(new ArmyList { Faction = FactionKey.Dwarfs, PointsTarget = 2000, Units = [unit] },
                TestCompositionRules.WapV2)
            .Where(i => i.Code == "FACTION_UPGRADES_OVER_LIMIT" || i.Code.StartsWith("MAGIC_ITEMS_"))
            .ToList();

    // ─── Remaining ───────────────────────────────────────────────

    [Theory]
    [InlineData("BOLT THROWER")]
    [InlineData("GRUDGE THROWER")]
    [InlineData("CANNON")]
    [InlineData("ORGAN GUN")]
    [InlineData("FLAME CANNON")]
    public async Task WarMachines_HaveTheirFiftyPointRuneLimit(string unitName)
    {
        var machine = await DwarfWith(unitName);

        Assert.Null(machine.ResolvedUnit.MagicItemAllowance);
        Assert.Equal(50, machine.FactionUpgradeLimitRemaining(Runes));
    }

    [Fact]
    public async Task Cannon_WithRapidFire_HasTwentyLeft()
    {
        var cannon = await Cannon("RUNE OF RAPID FIRE");

        Assert.Equal(20, cannon.FactionUpgradeLimitRemaining(Runes));
    }

    [Fact]
    public async Task Thane_HasNoOwnLimit_OnlyHisAllowance()
    {
        var thane = await DwarfWith("Thane");

        Assert.Null(thane.FactionUpgradeLimitRemaining(Runes));
        Assert.Equal(50, thane.ResolvedUnit.MagicItemAllowance);
    }

    // ─── Picker: what still fits ─────────────────────────────────

    [Fact]
    public async Task Cannon_WithRapidFire_OnlyARuneWithinTheLimitFits()
    {
        var cannon = await Cannon("RUNE OF RAPID FIRE");

        Assert.False(cannon.UpgradeFitsAllowance(Runes, await PickFor(cannon, "RUNE OF FORGING")));
        Assert.True(cannon.UpgradeFitsAllowance(Runes, await PickFor(cannon, "RUNE OF ACCURACY")));
    }

    [Fact]
    public async Task Cannon_RapidFireAlreadyPicked_IsNotCountedTwice()
    {
        var cannon = await Cannon("RUNE OF RAPID FIRE");

        Assert.True(cannon.UpgradeFitsAllowance(Runes, await PickFor(cannon, "RUNE OF RAPID FIRE")));
    }

    [Fact]
    public async Task EmptyCannon_BurstingFlameFits()
    {
        var cannon = await Cannon();

        Assert.True(cannon.UpgradeFitsAllowance(Runes, await PickFor(cannon, "MASTER RUNE OF BURSTING FLAME")));
    }

    [Fact]
    public async Task Thane_RunesStillSpendTheAllowance()
    {
        var thane = await DwarfWith("Thane", "RUNE OF RAPID FIRE");
        Assert.Equal(30, thane.AllowanceUpgradeCost);
        Assert.Equal(20, thane.MagicItemAllowanceRemaining);

        Assert.False(thane.UpgradeFitsAllowance(Runes, await PickFor(thane, "RUNE OF FORGING")));
        Assert.True(thane.UpgradeFitsAllowance(Runes, await PickFor(thane, "RUNE OF ACCURACY")));
    }

    // ─── Validation ──────────────────────────────────────────────

    [Fact]
    public async Task Cannon_OverItsLimit_FlagsOverLimit_NotAMagicItemIssue()
    {
        var cannon = await Cannon("RUNE OF RAPID FIRE", "RUNE OF FORGING");

        var issue = Assert.Single(LimitIssues(cannon));

        Assert.Equal("FACTION_UPGRADES_OVER_LIMIT", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("CANNON spends 55 on runic items, over its 50 pt limit", issue.Message);
    }

    [Fact]
    public async Task Cannon_AtItsLimit_NoIssue()
    {
        var cannon = await Cannon("RUNE OF RAPID FIRE", "RUNE OF ACCURACY");

        Assert.Empty(LimitIssues(cannon));
    }
}
