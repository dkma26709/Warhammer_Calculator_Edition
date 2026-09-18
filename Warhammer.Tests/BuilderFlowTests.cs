using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The Builder's flow end to end on the shipped bundle, as a player would drive it: pick a
/// faction, add entries, field a profile, take a mount and a magic item, validate, print, save
/// and reload. The Razor pages are not tested, so this is the nearest thing to opening them.
/// </summary>
public class BuilderFlowTests
{
    private static readonly ArmyListValidator Validator = new();

    [Fact]
    public async Task AnEmpireList_BuildsValidatesPrintsAndRoundTrips()
    {
        var repo = TestHelpers.Repo;
        var faction = await repo.GetFactionAsync("empire");
        var rules = await repo.GetCompositionRulesAsync("empire");
        var common = await repo.GetCommonMagicItemsAsync();
        var lores = await repo.GetLoresAsync("empire");
        Assert.NotEmpty(lores);

        // ── Add units ──
        var commanders = new UnitConfiguration(await TestHelpers.FindUnitAsync("empire", "COMMANDERS"));
        var halberdiers = new UnitConfiguration(await TestHelpers.FindUnitAsync("empire", "HALBERDIERS"))
            { ModelCount = 40, Frontage = 5 };
        var spearmen = new UnitConfiguration(await TestHelpers.FindUnitAsync("empire", "SPEARMEN"))
            { ModelCount = 20, Frontage = 5 };
        var list = new ArmyList
        {
            FactionId = "empire", PointsTarget = 1000, Name = "Smoke Test",
            Units = { commanders, halberdiers, spearmen },
        };

        // ── Field the Captain rather than the General ──
        Assert.Equal(2, commanders.ResolvedUnit.Profiles.Count);
        Assert.Equal("General", commanders.PrimaryProfile.Name);
        commanders.SelectedProfileId = "captain";
        Assert.Equal("Captain", commanders.PrimaryProfile.Name);
        Assert.Equal(55, commanders.BaseModelCost);
        Assert.Equal(50, commanders.MagicItemAllowance!.PointsLimit);

        // ── A mount from the book's mounts section ──
        var mountGroup = commanders.ActiveGroups().Single(g => g.IsKind(OptionKind.Mount));
        var warhorse = mountGroup.Choices.Single(c => c.Id == "warhorse");
        commanders.Selections.SelectOne(mountGroup.Id, warhorse.Id);
        Assert.NotNull(commanders.MountProfile);
        Assert.Equal(BaseTroopType.Cavalry, commanders.TroopType.BaseType);
        Assert.True(commanders.TroopType.IsCharacter);
        Assert.Equal(55 + 15, commanders.TotalPointsCost);

        // ── A magic item from the common list, within the allowance ──
        var sword = common.First(i => i.Category == MagicItemCategories.Weapon && i.Cost <= 50);
        commanders.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Id = sword.Id, Name = sword.Name, Category = sword.Category, PointsCost = sword.Cost ?? 0,
        });
        commanders.Loadout.IsGeneral = true;

        // ── Command and a banner on the Halberdiers ──
        halberdiers.HasChampion = true;
        halberdiers.HasStandardBearer = true;
        Assert.Equal(25, halberdiers.BannerBudget);
        var banner = faction.Items.Concat(common)
            .First(i => i.Category == MagicItemCategories.Standard && i.Cost <= 25);
        halberdiers.Loadout.SelectedBanner = new SelectedMagicItem
        {
            Id = banner.Id, Name = banner.Name, Category = banner.Category, PointsCost = banner.Cost ?? 0,
        };

        // ── Validation reads the faction's own rules and finds nothing wrong ──
        var issues = Validator.Validate(list, rules);
        Assert.Empty(issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message));

        // ── Composition in the Builder's summary bars ──
        var limits = rules.Resolve(list.Overrides, list.PointsTarget);
        Assert.Equal(350, limits.CharactersCombinedMax.Points);
        Assert.Equal(2, limits.SpecialDuplicateLimit);

        // ── Print ──
        var system = new GameSystemService();
        var roster = new ArmyRosterBuilder(repo, system).Build(list);
        Assert.Equal("The Empire", roster.FactionName);
        var characters = roster.Sections.Single(s => s.Title == "Characters");
        var captain = Assert.Single(characters.Entries);
        Assert.Equal("Captain", captain.Profiles[0].Name);
        Assert.Equal(2, captain.Profiles.Count);
        Assert.Contains("Warhorse", captain.Upgrades);
        Assert.Contains(captain.MagicItems, i => i.Points == (sword.Cost ?? 0));
        var core = roster.Sections.Single(s => s.Title == "Core");
        Assert.Equal(2, core.Entries.Count);
        Assert.Contains("Command: Leader, Standard Bearer", core.Entries.Single(e => e.Name == "Halberdiers").Notes);

        // ── Save and reload ──
        var serializer = new ArmyListSerializer(repo, system);
        var json = serializer.Serialize(list);
        var restored = await serializer.DeserializeAsync(json);
        Assert.True(restored.Success, string.Join("; ", restored.Errors));
        Assert.Empty(restored.Warnings);

        var back = restored.List!.Units.Single(u => u.ResolvedUnit.Id == "commanders");
        Assert.Equal("captain", back.SelectedProfileId);
        Assert.Equal(commanders.TotalPointsCost, back.TotalPointsCost);
        Assert.Equal(list.TotalPoints, restored.List.TotalPoints);
        Assert.Equal(issues.Count, Validator.Validate(restored.List, rules).Count);
    }

    /// <summary>A Dwarf character spends on runes through the family the book offers.</summary>
    [Fact]
    public async Task ADwarfCharacter_TakesRunesAgainstTheAllowance()
    {
        var repo = TestHelpers.Repo;
        var faction = await repo.GetFactionAsync("dwarfs");
        var lords = new UnitConfiguration(await TestHelpers.FindUnitAsync("dwarfs", "LORDS"));

        var offered = Assert.Single(lords.OfferedFamilies);
        Assert.Equal("runic-items", offered.Family);
        Assert.True(offered.ThroughMagicItems);

        var rune = FactionUpgrades.InFamily(faction.Upgrades, "runic-items").First(u => u.Cost?.Flat is > 0);
        var (points, _) = FactionUpgrades.ResolveCost(rune.Cost, lords.TroopType.IsCharacter, lords.TroopType.BaseType);
        lords.Loadout.SelectedFactionUpgrades["runic-items"] = [new SelectedFactionUpgrade(rune.Id, rune.Name, points)];

        Assert.Equal(points, lords.MagicItemSpend);
        Assert.Equal(lords.BaseModelCost + points, lords.TotalPointsCost);
    }

    /// <summary>Every faction's every entry can be configured and costed without throwing.</summary>
    [Fact]
    public async Task EveryEntry_ConfiguresAndCosts()
    {
        var repo = TestHelpers.Repo;
        foreach (var faction in repo.AllFactions)
        {
            var rules = await repo.GetCompositionRulesAsync(faction.Id);
            foreach (var unit in await repo.GetUnitsAsync(faction.Id))
            {
                var config = new UnitConfiguration(unit);
                foreach (var group in config.ActiveGroups().ToList())
                {
                    foreach (var choice in group.Choices)
                    {
                        if (group.GetSelectMode() == SelectMode.One)
                            config.Selections.SelectOne(group.Id, choice.Id);
                        else
                            config.Selections.Set(group.Id, choice.Id, Math.Min(1, config.MaxCountOf(choice)));
                    }
                }

                _ = config.TotalPointsCost;
                _ = config.EffectiveWeapons;
                _ = config.EffectiveArmour;
                _ = config.MagicItemAllowance;
                _ = config.OfferedFamilies.ToList();
                _ = config.MagicStandardAllowance;

                var list = new ArmyList { FactionId = faction.Id, PointsTarget = 2000, Units = { config } };
                _ = Validator.Validate(list, rules);
                _ = new ArmyRosterBuilder(repo, new GameSystemService()).Build(list);
            }
        }
    }
}
