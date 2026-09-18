using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The shipped data as the app reads it: the bundle for every enabled system, and the two
/// hand-maintained engine files beside it. A gap here is a unit that vanishes from a dropdown
/// or a chart the engine cannot read, and neither is loud at runtime.
/// </summary>
public class BundleDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string SystemDataDir(string systemKey) => TestPaths.SystemDataDir(systemKey);

    public static TheoryData<string> EnabledSystems()
    {
        var data = new TheoryData<string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
            data.Add(system.Key);
        return data;
    }

    private static BundleData LoadBundle(string systemKey)
    {
        var path = Path.Combine(SystemDataDir(systemKey), $"{systemKey}.json");
        Assert.True(File.Exists(path), $"{systemKey} has no bundle at {path}");
        return JsonSerializer.Deserialize<BundleData>(File.ReadAllText(path), JsonOptions)!;
    }

    /// <summary>Every (enabled system, faction id) pair, read off the bundles.</summary>
    public static TheoryData<string, string> SystemFactions()
    {
        var data = new TheoryData<string, string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
        {
            foreach (var faction in LoadBundle(system.Key).Factions.Keys)
                data.Add(system.Key, faction);
        }
        return data;
    }

    /// <summary>
    /// Entries the export gives one id to twice — a book that prints an entry in two sections.
    /// The format promises unique ids, so these are defects to fix upstream; listed rather than
    /// tolerated so a new one is loud. The repository keeps the first of each.
    /// </summary>
    private static readonly HashSet<string> KnownDuplicateUnitIds =
    [
        "albion/amanthas",
        "high-elves/great-eagle",
    ];

    // ─── The bundle ────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void Bundle_IsFormatVersion2_WithARulebookAndComposition(string systemKey)
    {
        var bundle = LoadBundle(systemKey);

        Assert.Equal(2, bundle.FormatVersion);
        Assert.Equal(systemKey, bundle.System);
        Assert.NotEmpty(bundle.Rulebook.Rules);
        Assert.NotEmpty(bundle.Rulebook.Items);
        Assert.NotEmpty(bundle.Rulebook.Lores);
        Assert.NotNull(bundle.Composition);
        Assert.NotEmpty(bundle.Factions);
    }

    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void Faction_Deserializes_WithUnitsProfilesAndIds(string systemKey, string factionId)
    {
        var faction = LoadBundle(systemKey).Factions[factionId].Deserialize<FactionData>(JsonOptions);

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
    [MemberData(nameof(SystemFactions))]
    public void EveryOptionGroup_HasAKnownKindAndSelectMode(string systemKey, string factionId)
    {
        var faction = LoadBundle(systemKey).Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
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

    /// <summary>Every magic item's category is one of the six the app files items under.</summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public async Task EveryMagicItem_HasAKnownCategoryAndACost(string systemKey, string factionId)
    {
        var bundle = LoadBundle(systemKey);
        var faction = bundle.Factions[factionId].Deserialize<FactionData>(JsonOptions)!;

        foreach (var item in bundle.Rulebook.Items.Concat(faction.Items))
        {
            Assert.True(MagicItemCategories.IsKnown(item.Category),
                $"{factionId}: {item.Name} has category '{item.Category}'");
            Assert.NotNull(item.Cost);
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"{factionId}: an item has no name");
        }

        // A faction's own items carry ids; the rulebook's common items do not yet, and the
        // loader derives one from the name.
        Assert.All(faction.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Id), $"{factionId}: {item.Name} has no id"));
        var common = await TestHelpers.RepoFor(systemKey).GetCommonMagicItemsAsync();
        Assert.All(common, item => Assert.False(string.IsNullOrWhiteSpace(item.Id), $"common item {item.Name} has no id"));
    }

    /// <summary>
    /// A faction upgrade family an option group names must exist as a chapter in the faction's
    /// upgrades, or the picker has nothing to offer.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void EveryOfferedUpgradeFamily_HasEntries(string systemKey, string factionId)
    {
        var faction = LoadBundle(systemKey).Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
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
    [MemberData(nameof(SystemFactions))]
    public void EveryUnitConstraint_NamesRealUnits(string systemKey, string factionId)
    {
        var faction = LoadBundle(systemKey).Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
        var unitIds = faction.Units.Select(u => u.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var constraint in faction.Composition?.UnitConstraints ?? [])
        {
            Assert.NotEmpty(constraint.UnitIds);
            Assert.All(constraint.UnitIds, id =>
                Assert.True(unitIds.Contains(id), $"{factionId}: constraint '{constraint.Raw}' names unknown unit '{id}'"));
        }
    }

    // ─── Through the repository ────────────────────────────────────

    /// <summary>
    /// A unit the repository cannot place — a section it does not map, a profile block it
    /// cannot read — is dropped without a word, so this compares what the repository returns
    /// against what the bundle holds, for every faction at once.
    /// </summary>
    [Theory]
    [MemberData(nameof(SystemFactions))]
    public async Task EveryUnitInASection_Resolves(string systemKey, string factionId)
    {
        var faction = LoadBundle(systemKey).Factions[factionId].Deserialize<FactionData>(JsonOptions)!;
        int inData = faction.Units.Count(u => u.Section is not null && u.Profiles.Count > 0);

        var resolved = await TestHelpers.RepoFor(systemKey).GetUnitsAsync(factionId);

        Assert.Equal(inData, resolved.Count);
        Assert.All(resolved, u => Assert.False(string.IsNullOrEmpty(u.Id)));
    }

    /// <summary>The faction list the app shows comes from the bundle, and every entry loads.</summary>
    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public async Task Repository_ListsEveryFactionInTheBundle(string systemKey)
    {
        var repo = TestHelpers.RepoFor(systemKey);
        var bundle = LoadBundle(systemKey);

        Assert.Equal(bundle.Factions.Keys.OrderBy(k => k), repo.AllFactions.Select(f => f.Id).OrderBy(k => k));
        foreach (var faction in repo.AllFactions)
        {
            Assert.False(string.IsNullOrWhiteSpace(faction.Name));
            Assert.Equal(faction.Name, repo.GetFactionDisplayName(faction.Id));
            Assert.NotEmpty(await repo.GetUnitsAsync(faction.Id));
        }
    }

    /// <summary>Every faction the bundle has is one the engine knows a race for, or Hatred silently never applies.</summary>
    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void EveryFaction_HasARaceEntry(string systemKey)
    {
        var known = Calculator.Models.FactionRaces.KnownFactions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = LoadBundle(systemKey).Factions.Keys.Where(k => !known.Contains(k)).ToList();

        Assert.Empty(missing);
    }

    // ─── The engine's own files ────────────────────────────────────

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void CoreRules_Deserializes(string systemKey)
    {
        var json = File.ReadAllText(Path.Combine(SystemDataDir(systemKey), "core-rules.json"));
        var data = JsonSerializer.Deserialize<CoreRulesData>(json, JsonOptions);
        Assert.NotNull(data);
        Assert.NotEmpty(data.CloseCombatToHit.Table);
        Assert.NotEmpty(data.Weapons.CloseCombat);
    }

    /// <summary>
    /// Every lookup the combat engine reads on a hot path, asserted per system. A null toWound
    /// table or an empty strengthModifiers map deserialises fine and only surfaces as a
    /// NullReferenceException mid-render.
    /// </summary>
    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void CoreRules_CarryEveryLookupTheEngineNeeds(string systemKey)
    {
        var json = File.ReadAllText(Path.Combine(SystemDataDir(systemKey), "core-rules.json"));
        var data = JsonSerializer.Deserialize<CoreRulesData>(json, JsonOptions)!;

        AssertSquareTable(data.CloseCombatToHit.Table, $"{systemKey} closeCombatToHit.table",
            allowCompound: false);
        AssertSquareTable(data.ToWound.Table, $"{systemKey} toWound.table",
            allowCompound: true);

        Assert.NotNull(data.ArmourSaves.StrengthModifiers);
        for (int strength = 1; strength <= 10; strength++)
        {
            Assert.True(data.ArmourSaves.StrengthModifiers.ContainsKey($"S{strength}"),
                $"{systemKey} armourSaves.strengthModifiers has no S{strength} — " +
                "high-strength attacks would silently ignore armour penalties");
        }

        Assert.NotEmpty(data.Armour);
    }

    private static void AssertSquareTable(string[][] table, string what, bool allowCompound)
    {
        Assert.NotNull(table);
        Assert.Equal(10, table.Length);
        foreach (var row in table)
        {
            Assert.NotNull(row);
            Assert.Equal(10, row.Length);
        }

        foreach (var cell in table.SelectMany(r => r))
        {
            Assert.True(RollRequirement.TryParse(cell, out var requirement),
                $"{what} holds '{cell}', which is not a chart score");
            Assert.True(allowCompound || !requirement.NeedsSecondRoll,
                $"{what} holds compound score '{cell}', which only the to-wound chart supports");
        }
    }

    /// <summary>The engine's to-hit and to-wound charts must match the rulebook the bundle prints.</summary>
    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void CoreRules_ChartsMatchTheRulebookText(string systemKey)
    {
        var json = File.ReadAllText(Path.Combine(SystemDataDir(systemKey), "core-rules.json"));
        var data = JsonSerializer.Deserialize<CoreRulesData>(json, JsonOptions)!;

        var bundleJson = File.ReadAllText(Path.Combine(SystemDataDir(systemKey), $"{systemKey}.json"));
        using var bundle = JsonDocument.Parse(bundleJson);

        var toHit = FindChart(bundle.RootElement.GetProperty("rulebook").GetProperty("chapters"), "ROLL TO HIT");
        var toWound = FindChart(bundle.RootElement.GetProperty("rulebook").GetProperty("chapters"), "ROLL TO WOUND");

        Assert.Equal(toHit, data.CloseCombatToHit.Table.Select(r => r.ToList()).ToList());
        Assert.Equal(toWound, data.ToWound.Table.Select(r => r.ToList()).ToList());
    }

    /// <summary>The 10×10 chart printed under a heading, as rows of cells: "1 | 4+ | 4+ | …".</summary>
    private static List<List<string>> FindChart(JsonElement chapters, string title)
    {
        string? text = null;
        void Walk(JsonElement node)
        {
            if (text != null) return;
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("title", out var t) && t.GetString() == title
                    && node.TryGetProperty("text", out var body))
                {
                    text = body.GetString();
                    return;
                }
                foreach (var property in node.EnumerateObject()) Walk(property.Value);
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray()) Walk(item);
            }
        }
        Walk(chapters);
        Assert.NotNull(text);

        var rows = new List<List<string>>();
        foreach (var line in text.Split('\n'))
        {
            var cells = line.Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count == 11 && int.TryParse(cells[0], out var row) && row == rows.Count + 1)
                rows.Add(cells.Skip(1).ToList());
        }

        Assert.Equal(10, rows.Count);
        return rows;
    }

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void TroopTypes_CarryEveryBaseType(string systemKey)
    {
        var json = File.ReadAllText(Path.Combine(SystemDataDir(systemKey), "troop-types.json"));
        var data = JsonSerializer.Deserialize<TroopTypesData>(json, JsonOptions)!;

        foreach (var type in Enum.GetValues<BaseTroopType>())
            Assert.True(data.Types.ContainsKey(type.ToJsonKey()), $"{systemKey} troop-types.json lacks {type.ToJsonKey()}");
    }
}
