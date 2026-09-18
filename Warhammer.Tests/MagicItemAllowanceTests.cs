using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// An army book prints one options block per page, so an entry carries its neighbours'
/// allowances alongside its own, each naming the profile it belongs to. These cover picking
/// the right one, and the shipped data actually offering one wherever the book does.
/// </summary>
public class MagicItemAllowanceTests
{
    [Fact]
    public void NoAllowanceGroup_ResolvesToNull()
    {
        Assert.Null(Unit.ResolveMagicItemAllowance("captain", []));
        Assert.Null(Unit.ResolveMagicItemAllowance("captain", null));
    }

    [Fact]
    public void UnnamedAllowance_AppliesToWhoeverHoldsIt()
    {
        Assert.Equal(50, Unit.ResolveMagicItemAllowance("captain", [TestHelpers.MagicItems(50)])!.PointsLimit);
    }

    [Fact]
    public void NamedAllowance_MatchingTheProfile_Wins()
    {
        // The Empire's COMMANDERS entry carries both the Captain's line and the General's.
        var groups = new List<OptionGroup> { TestHelpers.MagicItems(50, "captain"), TestHelpers.MagicItems(100, "general") };

        Assert.Equal(100, Unit.ResolveMagicItemAllowance("general", groups)!.PointsLimit);
        Assert.Equal(50, Unit.ResolveMagicItemAllowance("captain", groups)!.PointsLimit);
    }

    [Fact]
    public void NamedAllowance_MatchesRegardlessOfCase()
    {
        var groups = new List<OptionGroup> { TestHelpers.MagicItems(100, "Battle-Wizard-Lord") };

        Assert.Equal(100, Unit.ResolveMagicItemAllowance("battle-wizard-lord", groups)!.PointsLimit);
    }

    [Fact]
    public void UnnamedAllowance_BeatsANeighboursNamedOne()
    {
        var groups = new List<OptionGroup> { TestHelpers.MagicItems(100, "grand-master"), TestHelpers.MagicItems(50) };

        Assert.Equal(50, Unit.ResolveMagicItemAllowance("chapter-master", groups)!.PointsLimit);
    }

    [Fact]
    public void OnlyNeighboursAllowances_TakesTheSmallest()
    {
        // Nothing names this profile, so the book never granted it the larger budget.
        var groups = new List<OptionGroup> { TestHelpers.MagicItems(100, "prince"), TestHelpers.MagicItems(50, "noble") };

        Assert.Equal(50, Unit.ResolveMagicItemAllowance("loremaster", groups)!.PointsLimit);
    }

    [Fact]
    public void AnAllowance_CarriesItsListsAndFamilies()
    {
        var group = TestHelpers.MagicItems(50, itemTypes: ["talisman"], families: ["virtues-of-the-chivalric-knight"]);
        var allowance = Unit.ResolveMagicItemAllowance(null, [group])!;

        Assert.True(allowance.Admits(MagicItemCategories.Talisman));
        Assert.False(allowance.Admits(MagicItemCategories.Weapon));
        Assert.Equal(["virtues-of-the-chivalric-knight"], allowance.Families);
    }

    [Fact]
    public void AnAllowanceNamingNoLists_AdmitsAny()
    {
        var allowance = Unit.ResolveMagicItemAllowance(null, [TestHelpers.MagicItems(50)])!;

        Assert.True(allowance.Admits(MagicItemCategories.Weapon));
        Assert.True(allowance.Admits(MagicItemCategories.Enchanted));
    }

    // ─── Through the configuration ──────────────────────────────────

    [Fact]
    public void TheFieldedProfile_DecidesTheAllowance()
    {
        var general = new ProfileEntry { Id = "general", Name = "General", Points = 95, WS = 5 };
        var captain = new ProfileEntry { Id = "captain", Name = "Captain", Points = 55, WS = 4 };
        var unit = TestHelpers.MakeUnit(name: "Commanders", troopType: "Infantry (Character)",
            profiles: [general, captain],
            options: [TestHelpers.MagicItems(50, "captain"), TestHelpers.MagicItems(100, "general")]);

        var config = new UnitConfiguration(unit);
        Assert.Equal(100, config.MagicItemAllowance!.PointsLimit);

        config.SelectedProfileId = "captain";
        Assert.Equal(50, config.MagicItemAllowance!.PointsLimit);
        Assert.Equal(55, config.BaseModelCost);
    }

    [Fact]
    public void FamilyPicks_CountAgainstTheAllowance()
    {
        var unit = TestHelpers.MakeUnit(name: "Paladin", troopType: "Infantry (Character)", points: 100,
            options: [TestHelpers.MagicItems(100, families: ["virtues"])]);
        var config = new UnitConfiguration(unit);
        config.SelectedMagicItems.Add(new SelectedMagicItem { Name = "Sword", Category = "weapon", PointsCost = 40 });
        config.Loadout.SelectedFactionUpgrades["virtues"] = [new SelectedFactionUpgrade("virtue-of-valour", "Virtue of Valour", 35)];

        Assert.Equal(75, config.MagicItemSpend);
        Assert.Equal(175, config.TotalPointsCost);
    }

    // ─── Shipped data ───────────────────────────────────────────────

    public static TheoryData<string, string> SystemFactions() => BundleDataTests.SystemFactions();

    /// <summary>
    /// Every profile a magic item allowance names must be a profile of its entry, or the
    /// allowance can never apply.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public async Task EveryNamedAllowance_NamesAProfileOfItsEntry(string systemKey, string faction)
    {
        var units = await TestHelpers.RepoFor(systemKey).GetUnitsAsync(faction);
        var stragglers = new List<string>();

        foreach (var unit in units)
        {
            var profileIds = unit.RawEntry.Profiles.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (group, _) in unit.OptionGroups.Flatten())
            {
                if (!group.IsKind(OptionKind.MagicItems) || group.AppliesTo is null) continue;
                if (!profileIds.Contains(group.AppliesTo))
                    stragglers.Add($"{unit.Name}: allowance for '{group.AppliesTo}' ({group.Raw})");
            }
        }

        Assert.Empty(stragglers);
    }

    /// <summary>
    /// Every entry the book gives an allowance to must resolve to a budget for each of its
    /// priced profiles, or the builder shows no magic item picker at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public async Task EntriesWithAnAllowance_ResolveToABudget(string systemKey, string faction)
    {
        var units = await TestHelpers.RepoFor(systemKey).GetUnitsAsync(faction);

        foreach (var unit in units)
        {
            if (!unit.OptionGroups.Any(g => g.IsKind(OptionKind.MagicItems)))
                continue;

            foreach (var profile in unit.Profiles)
            {
                var config = new UnitConfiguration(unit) { SelectedProfileId = profile.Id };
                var resolved = config.MagicItemAllowance;

                Assert.True(resolved is not null,
                    $"{systemKey}/{faction}: {unit.Name} ({profile.Name}) has allowance groups but resolved to none");
            }
        }
    }
}
