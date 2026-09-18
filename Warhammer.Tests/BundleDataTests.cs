using System.Text.Json;
using Warhammer.Models.Bundle;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

/// <summary>
/// The shipped bundle as the models read it. A gap here is a unit that vanishes from a dropdown
/// or an option group that renders nothing, and neither is loud at runtime.
/// </summary>
public class BundleDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The bundle the models are written against; not yet a system the app offers.</summary>
    private const string BundleKey = "war";

    private static BundleData LoadBundle()
    {
        var path = Path.Combine(TestPaths.SystemDataDir(BundleKey), $"{BundleKey}.json");
        Assert.True(File.Exists(path), $"{BundleKey} has no bundle at {path}");
        return JsonSerializer.Deserialize<BundleData>(File.ReadAllText(path), JsonOptions)!;
    }

    /// <summary>Every faction id in the bundle.</summary>
    public static TheoryData<string> Factions()
    {
        var data = new TheoryData<string>();
        foreach (var faction in LoadBundle().Factions.Keys)
            data.Add(faction);
        return data;
    }

    /// <summary>
    /// Entries the export gives one id to twice — a book that prints an entry in two sections.
    /// The format promises unique ids, so these are defects to fix upstream; listed rather than
    /// tolerated so a new one is loud.
    /// </summary>
    private static readonly HashSet<string> KnownDuplicateUnitIds =
    [
        "albion/amanthas",
        "high-elves/great-eagle",
    ];

    [Fact]
    public void Bundle_IsFormatVersion2_WithARulebookAndComposition()
    {
        var bundle = LoadBundle();

        Assert.Equal(2, bundle.FormatVersion);
        Assert.Equal(BundleKey, bundle.System);
        Assert.NotEmpty(bundle.Rulebook.Rules);
        Assert.NotEmpty(bundle.Rulebook.Items);
        Assert.NotEmpty(bundle.Rulebook.Lores);
        Assert.NotNull(bundle.Composition);
        Assert.NotEmpty(bundle.Factions);
    }

    [Theory]
    [MemberData(nameof(Factions))]
    public void Faction_Deserializes_WithUnitsProfilesAndIds(string factionId)
    {
        var faction = LoadBundle().Factions[factionId].Deserialize<FactionData>(JsonOptions);

        Assert.NotNull(faction);
        Assert.Equal(factionId, faction.Id);
        Assert.False(string.IsNullOrWhiteSpace(faction.Name));
        Assert.NotEmpty(faction.Units);

        var ids = new HashSet<string>();
        foreach (var unit in faction.Units)
        {
            Assert.False(string.IsNullOrWhiteSpace(unit.Name), "Unit name should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(unit.Id), $"{unit.Name} has no id");
            Assert.True(ids.Add(unit.Id) || KnownDuplicateUnitIds.Contains($"{factionId}/{unit.Id}"),
                $"{factionId}: unit id '{unit.Id}' is used twice");
            Assert.NotEmpty(unit.Profiles);
            Assert.All(unit.Profiles, p => Assert.False(string.IsNullOrWhiteSpace(p.Id), $"{unit.Name}: profile '{p.Name}' has no id"));
        }
    }

    /// <summary>
    /// Every option group's kind and selection mode is one this build knows, at every nesting
    /// level. A kind it does not know renders nothing and costs nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Factions))]
    public void EveryOptionGroup_HasAKnownKindAndSelectMode(string factionId)
    {
        var faction = LoadBundle().Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
        var unknown = new List<string>();

        foreach (var unit in faction.Units)
        {
            var groupIds = new HashSet<string>();
            foreach (var (group, _) in unit.OptionGroups.Flatten())
            {
                if (group.GetKind() is null)
                    unknown.Add($"{unit.Name}: kind '{group.Kind}' on '{group.Id}'");
                if (group.Select is not ("one" or "any" or "count"))
                    unknown.Add($"{unit.Name}: select '{group.Select}' on '{group.Id}'");
                if (!groupIds.Add(group.Id))
                    unknown.Add($"{unit.Name}: group id '{group.Id}' is used twice");

                var choiceIds = new HashSet<string>();
                foreach (var choice in group.Choices)
                {
                    if (!choiceIds.Add(choice.Id))
                        unknown.Add($"{unit.Name}/{group.Id}: choice id '{choice.Id}' is used twice");
                }
            }
        }

        Assert.Empty(unknown);
    }

    /// <summary>
    /// A faction upgrade family an option group names must exist as a chapter in the faction's
    /// upgrades, or the picker has nothing to offer.
    /// </summary>
    [Theory]
    [MemberData(nameof(Factions))]
    public void EveryOfferedUpgradeFamily_HasEntries(string factionId)
    {
        var faction = LoadBundle().Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
        var missing = new List<string>();

        foreach (var unit in faction.Units)
        {
            foreach (var (group, _) in unit.OptionGroups.Flatten())
            {
                var families = new List<string>();
                if (group.IsKind(OptionKind.FactionUpgrade) && group.Family is { } family)
                    families.Add(family);
                if (group.Families is { } more)
                    families.AddRange(more);

                foreach (var name in families)
                {
                    if (!FactionUpgrades.InFamily(faction.Upgrades, name).Any())
                        missing.Add($"{unit.Name}: family '{name}'");
                }
            }
        }

        Assert.Empty(missing);
    }

    /// <summary>Every unit constraint names units the faction has.</summary>
    [Theory]
    [MemberData(nameof(Factions))]
    public void EveryUnitConstraint_NamesRealUnits(string factionId)
    {
        var faction = LoadBundle().Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
        var unitIds = faction.Units.Select(u => u.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var constraint in faction.Composition?.UnitConstraints ?? [])
        {
            Assert.NotEmpty(constraint.UnitIds);
            Assert.All(constraint.UnitIds, id =>
                Assert.True(unitIds.Contains(id), $"{factionId}: constraint '{constraint.Raw}' names unknown unit '{id}'"));
        }
    }
}
