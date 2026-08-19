using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class ArmyListSerializerTests
{
    // Lightweight IGameDataRepository stub — only the methods the serializer calls.
    private sealed class StubRepo : IGameDataRepository
    {
        private readonly IReadOnlyList<Unit> _units;
        public StubRepo(params Unit[] units) => _units = units;

        /// <summary>Faction upgrade entries the serializer resolves pick costs from.</summary>
        public FactionExtrasData Extras { get; init; } = new();

        public Task EnsureLoadedAsync() => Task.CompletedTask;
        public IReadOnlyList<FactionKey> AllFactions => [];
        public string GetFactionDisplayName(FactionKey faction) => faction.ToString();
        public Task<IReadOnlyList<Unit>> GetUnitsAsync(FactionKey faction) => Task.FromResult(_units);
        public Task<FactionExtrasData> GetFactionExtrasAsync(FactionKey faction) =>
            Task.FromResult(Extras);
        public Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync() =>
            Task.FromResult<IReadOnlyList<MagicItemEntry>>([]);
        public Task<CompositionRules> GetCompositionRulesAsync() =>
            Task.FromResult(TestCompositionRules.WapV2);
        public Weapon? GetWeapon(CloseCombatWeaponKey key) => null;
        public Weapon GetHandWeapon() => throw new NotImplementedException();
        public Armour? GetArmour(ArmourKey key) => null;
        public RollRequirement GetToHitRequirement(int attackerWS, int defenderWS) => RollRequirement.Plain(4);
        public RollRequirement GetToWoundRequirement(int strength, int toughness) => RollRequirement.Plain(4);
        public int GetArmourSaveModifier(int strength) => 0;
    }

    [Fact]
    public void Serialize_ProducesSchemaAndFactionAndUnits()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Name = "Test List",
            Units = { new UnitConfiguration(unit) { ModelCount = 25 } },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);

        Assert.Contains("wap-army-list/v1", json);
        Assert.Contains("dwarfs", json);
        Assert.Contains("\"pointsTarget\": 2000", json);
        Assert.Contains("Warriors", json);
    }

    // ─── Composition overrides ──────────────────────────────────────

    [Fact]
    public void Serialize_OmitsComposition_WhenNothingIsOverridden()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = new ArmyList { Faction = FactionKey.Dwarfs, PointsTarget = 2000 };

        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService()).Serialize(list);

        Assert.DoesNotContain("composition", json);
    }

    [Fact]
    public async Task Composition_RoundTripsThroughExportAndImport()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { new UnitConfiguration(unit) { ModelCount = 10 } },
            Overrides = new CompositionOverrides
            {
                RareMax = new() { Value = 15, Unit = LimitUnit.Percent },
                CoreMin = new() { Value = 600, Unit = LimitUnit.Points },
                SingleUnitMax = new() { Value = 400, Unit = LimitUnit.Points },
                SpecialDuplicateLimit = 1,
            },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);

        // The unit must survive as a name, not an enum ordinal.
        Assert.Contains("\"Percent\"", json);
        Assert.Contains("\"Points\"", json);

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var restored = result.List!.Overrides;
        Assert.Equal(15, restored.RareMax!.Value);
        Assert.Equal(LimitUnit.Percent, restored.RareMax.Unit);
        Assert.Equal(600, restored.CoreMin!.Value);
        Assert.Equal(LimitUnit.Points, restored.CoreMin.Unit);
        Assert.Equal(400, restored.SingleUnitMax!.Value);
        Assert.Equal(LimitUnit.Points, restored.SingleUnitMax.Unit);
        Assert.Equal(1, restored.SpecialDuplicateLimit);
        Assert.Null(restored.LordsMax);
        Assert.Null(restored.RareDuplicateLimit);
    }

    [Fact]
    public async Task LegacySingleCharacterOverride_LandsOnSingleUnitMax()
    {
        // A list saved while the cap was still character-only used "singleCharacterMax".
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var json = """
        {
          "schema": "wap-army-list/v1",
          "system": "wap-v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy List",
          "units": [ { "unitName": "Warriors", "modelCount": 10, "frontage": 5 } ],
          "composition": { "singleCharacterMax": { "value": 400, "unit": "Points" } }
        }
        """;

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Equal(400, result.List!.Overrides.SingleUnitMax!.Value);
        Assert.Equal(LimitUnit.Points, result.List.Overrides.SingleUnitMax.Unit);

        // Re-exporting writes the current name only.
        var rewritten = serializer.Serialize(result.List);
        Assert.Contains("singleUnitMax", rewritten);
        Assert.DoesNotContain("singleCharacterMax", rewritten);
    }

    [Fact]
    public async Task ListSavedWithoutComposition_ImportsWithNoOverrides()
    {
        // A wap-army-list/v1 file written before overrides existed.
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var json = """
        {
          "schema": "wap-army-list/v1",
          "system": "wap-v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy List",
          "units": [ { "unitName": "Warriors", "modelCount": 10, "frontage": 5 } ]
        }
        """;

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Empty(result.Warnings);
        Assert.True(result.List!.Overrides.IsEmpty);
    }

    [Fact]
    public async Task DeserializeAsync_RoundTripsAllFields()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8, unitSize: "10+");
        var original = new UnitConfiguration(unit)
        {
            ModelCount = 25,
            Frontage = 5,
            HasChampion = true,
            HasMusician = true,
            HasStandardBearer = false,
            Loadout = { WizardLevel = 3, SelectedLore = "Lore of Fire" },
        };
        original.Loadout.SelectedSpells.Add("Fireball");
        original.Loadout.SelectedSpells.Add("Flame Storm");

        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 1500,
            Name = "Roundtrip Army",
            Units = { original },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);
        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Equal(FactionKey.Dwarfs, result.List!.Faction);
        Assert.Equal(1500, result.List.PointsTarget);
        Assert.Equal("Roundtrip Army", result.List.Name);
        Assert.Single(result.List.Units);

        var copy = result.List.Units[0];
        Assert.Equal("Warriors", copy.ResolvedUnit.Name);
        Assert.Equal(25, copy.ModelCount);
        Assert.Equal(5, copy.Frontage);
        Assert.True(copy.HasChampion);
        Assert.True(copy.HasMusician);
        Assert.False(copy.HasStandardBearer);
        Assert.Equal(3, copy.Loadout.WizardLevel);
        Assert.Equal("Lore of Fire", copy.Loadout.SelectedLore);
        Assert.Equal(new[] { "Fireball", "Flame Storm" }, copy.Loadout.SelectedSpells);
    }

    [Fact]
    public async Task DeserializeAsync_InvalidJson_ReturnsError()
    {
        var serializer = new ArmyListSerializer(new StubRepo(), new GameSystemService());
        var result = await serializer.DeserializeAsync("{ this is not json");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.StartsWith("Invalid JSON"));
    }

    [Fact]
    public async Task DeserializeAsync_UnknownFaction_ReturnsError()
    {
        var serializer = new ArmyListSerializer(new StubRepo(), new GameSystemService());
        var json = """{"schema":"wap-army-list/v1","faction":"not-a-faction","pointsTarget":2000,"name":"x","units":[]}""";

        var result = await serializer.DeserializeAsync(json);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Unknown faction"));
    }

    [Fact]
    public async Task DeserializeAsync_UnknownUnit_WarnsAndSkips()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {
          "schema": "wap-army-list/v1",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Test",
          "units": [
            { "unitName": "Warriors", "modelCount": 10 },
            { "unitName": "Ghost Unit", "modelCount": 1 }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Single(result.List!.Units);
        Assert.Equal("Warriors", result.List.Units[0].ResolvedUnit.Name);
        Assert.Contains(result.Warnings, w => w.Contains("Ghost Unit"));
    }

    [Fact]
    public async Task DeserializeAsync_SchemaMismatch_WarnsButContinues()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {"schema":"wap-army-list/v0","faction":"dwarfs","pointsTarget":1000,"name":"Old","units":[]}
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Schema"));
    }

    [Fact]
    public void Serialize_IncludesCurrentSystem()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { new UnitConfiguration(unit) },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);

        Assert.Contains("\"system\": \"wap-v2\"", json);
    }

    [Fact]
    public async Task DeserializeAsync_MissingSystemField_TreatedAsWapV2()
    {
        // Exports from before multi-system support have no "system" property.
        var serializer = new ArmyListSerializer(new StubRepo(), new GameSystemService());
        var json = """{"schema":"wap-army-list/v1","faction":"dwarfs","pointsTarget":2000,"name":"Legacy","units":[]}""";

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("system"));
    }

    [Fact]
    public async Task DeserializeAsync_DifferentSystem_WarnsButContinues()
    {
        var serializer = new ArmyListSerializer(new StubRepo(), new GameSystemService());
        var json = """{"schema":"wap-army-list/v1","system":"old-world","faction":"dwarfs","pointsTarget":2000,"name":"X","units":[]}""";

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("saved for system 'old-world'"));
    }

    [Fact]
    public async Task DeserializeAsync_FactionUpgrades_ResolvesCostsFromExtras()
    {
        // Saved lists store upgrade names only; costs are re-resolved from faction data.
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var repo = new StubRepo(warriors)
        {
            Extras = new FactionExtrasData
            {
                FactionUpgrades = new Dictionary<string, List<UpgradeEntry>>
                {
                    ["blessed_spawnings"] =
                    [
                        new UpgradeEntry { Name = "Blessed Spawning Of Chotec", Points = "10" },
                        new UpgradeEntry { Name = "Blessed Spawning Of Itzl", Points = "5" },
                    ],
                },
            },
        };
        var serializer = new ArmyListSerializer(repo, new GameSystemService());
        var json = """
        {
          "schema": "wap-army-list/v1",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy",
          "units": [
            {
              "unitName": "Warriors",
              "modelCount": 10,
              "selectedFactionUpgrades": { "blessed_spawnings": ["Blessed Spawning Of Itzl"] }
            }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var picks = result.List!.Units[0].Loadout.SelectedFactionUpgrades["blessed_spawnings"];
        Assert.Equal("Blessed Spawning Of Itzl", Assert.Single(picks).Name);
        Assert.Equal(5, picks[0].PointsCost);
    }

    [Fact]
    public async Task DeserializeAsync_FactionUpgrades_UnknownName_ImportsAtZeroCost()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {
          "schema": "wap-army-list/v1",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy",
          "units": [
            {
              "unitName": "Warriors",
              "modelCount": 10,
              "selectedFactionUpgrades": { "blessed_spawnings": ["Retired Spawning"] }
            }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var picks = result.List!.Units[0].Loadout.SelectedFactionUpgrades["blessed_spawnings"];
        Assert.Equal(0, Assert.Single(picks).PointsCost);
    }

    [Fact]
    public void Serialize_FactionUpgrades_WritesNamesOnly()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Loadout.SelectedFactionUpgrades["blessed_spawnings"] =
        [
            new SelectedFactionUpgrade("Blessed Spawning Of Chotec", 10),
        ];
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { config },
        };

        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService()).Serialize(list);

        Assert.Contains("\"Blessed Spawning Of Chotec\"", json);
        Assert.DoesNotContain("pointsCost", json);
    }

    [Fact]
    public async Task DeserializeAsync_RoundTripsCustomName()
    {
        var unit = TestHelpers.MakeUnit(name: "Empire Captain", points: 65);
        var config = new UnitConfiguration(unit)
        {
            ModelCount = 1,
            Loadout = { CustomName = "Aldric the Bold" },
        };
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { config },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);
        Assert.Contains("Aldric the Bold", json);

        var result = await serializer.DeserializeAsync(json);
        Assert.True(result.Success);
        Assert.Equal("Aldric the Bold", result.List!.Units[0].Loadout.CustomName);
    }

    [Fact]
    public void Serialize_OmitsCustomNameWhenNullOrBlank()
    {
        var unit = TestHelpers.MakeUnit(name: "Empire Captain", points: 65);
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { new UnitConfiguration(unit) { Loadout = { CustomName = "   " } } },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);

        Assert.DoesNotContain("customName", json);
    }

    [Fact]
    public void Serialize_IncludesBannerWhenPresent()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var config = new UnitConfiguration(warriors)
        {
            ModelCount = 20,
            Loadout =
            {
                SelectedBanner = new SelectedMagicItem
                {
                    Name = "Razor Standard",
                    Category = "magic_standard",
                    PointsCost = 45,
                },
            },
        };
        var list = new ArmyList
        {
            Faction = FactionKey.Dwarfs,
            PointsTarget = 2000,
            Units = { config },
        };

        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = serializer.Serialize(list);

        Assert.Contains("Razor Standard", json);
        Assert.Contains("magic_standard", json);
    }
}
