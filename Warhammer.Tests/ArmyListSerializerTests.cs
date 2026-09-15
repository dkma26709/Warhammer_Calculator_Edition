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

        /// <summary>The faction's upgrade entries the serializer resolves pick costs from.</summary>
        public List<UpgradeEntry> Upgrades { get; init; } = [];

        public Task EnsureLoadedAsync() => Task.CompletedTask;
        public IReadOnlyList<Faction> AllFactions => [new("dwarfs", "Dwarfs", "order"), new("lizardmen", "Lizardmen", "order")];
        public string GetFactionDisplayName(string factionId) => factionId;
        public Task<IReadOnlyList<Unit>> GetUnitsAsync(string factionId) => Task.FromResult(_units);
        public Task<FactionData> GetFactionAsync(string factionId) =>
            Task.FromResult(new FactionData { Id = factionId, Upgrades = Upgrades });
        public Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync() =>
            Task.FromResult<IReadOnlyList<MagicItemEntry>>([]);
        public Task<IReadOnlyList<LoreEntry>> GetLoresAsync(string factionId) =>
            Task.FromResult<IReadOnlyList<LoreEntry>>([]);
        public Task<CompositionRules> GetCompositionRulesAsync(string? factionId = null) =>
            Task.FromResult(TestCompositionRules.War);
        public Weapon? GetWeapon(CloseCombatWeaponKey key) => null;
        public Weapon GetHandWeapon() => throw new NotImplementedException();
        public Armour? GetArmour(ArmourKey key) => null;
        public RollRequirement GetToHitRequirement(int attackerWS, int defenderWS) => RollRequirement.Plain(4);
        public RollRequirement GetToWoundRequirement(int strength, int toughness) => RollRequirement.Plain(4);
        public int GetArmourSaveModifier(int strength) => 0;
    }

    private static ArmyList DwarfList(params UnitConfiguration[] units) => new()
    {
        FactionId = "dwarfs",
        PointsTarget = 2000,
        Name = "Test List",
        Units = units.ToList(),
    };

    [Fact]
    public void Serialize_ProducesSchemaFactionAndUnitIds()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = DwarfList(new UnitConfiguration(unit) { ModelCount = 25 });

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);

        Assert.Contains("army-list/v2", json);
        Assert.Contains("\"faction\": \"dwarfs\"", json);
        Assert.Contains("\"pointsTarget\": 2000", json);
        Assert.Contains("\"unitId\": \"warriors\"", json);
        Assert.Contains("\"unitName\": \"Warriors\"", json);
    }

    // ─── Composition overrides ──────────────────────────────────────

    [Fact]
    public void Serialize_OmitsComposition_WhenNothingIsOverridden()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = DwarfList();

        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService()).Serialize(list);

        Assert.DoesNotContain("composition", json);
    }

    [Fact]
    public async Task Composition_RoundTripsThroughExportAndImport()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var list = DwarfList(new UnitConfiguration(unit) { ModelCount = 10 });
        list.Overrides = new CompositionOverrides
        {
            RareMax = new() { Value = 15, Unit = LimitUnit.Percent },
            CoreMin = new() { Value = 600, Unit = LimitUnit.Points },
            SingleUnitMax = new() { Value = 400, Unit = LimitUnit.Points },
            SpecialDuplicateLimit = 1,
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

    // ─── Selections ─────────────────────────────────────────────────

    [Fact]
    public async Task Selections_RoundTripByGroupAndChoiceId()
    {
        var unit = TestHelpers.MakeUnit(name: "Stegadon", points: 215, options:
        [
            TestHelpers.Equipment("upgrades", SelectMode.Any,
                TestHelpers.Choice("Unstoppable Stampede", 10),
                TestHelpers.Choice("Ancient", 30)),
            TestHelpers.Equipment("shield", 1, perModel: true),
        ]);
        var original = new UnitConfiguration(unit);
        original.Selections.Toggle("upgrades", "unstoppable-stampede", true);
        original.Selections.Toggle("upgrades", "ancient", true);
        original.Selections.Toggle("shield", "shield", true);

        var list = new ArmyList { FactionId = "lizardmen", PointsTarget = 2000, Units = { original } };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);
        Assert.Contains("\"unstoppable-stampede\": 1", json);

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var restored = result.List!.Units.Single();
        Assert.Equal(["unstoppable-stampede", "ancient"], restored.Selections.SelectedIn("upgrades"));
        Assert.True(restored.Selections.IsSelected("shield", "shield"));
        Assert.Equal(original.TotalPointsCost, restored.TotalPointsCost);
    }

    [Fact]
    public async Task ACountSelection_KeepsItsCount()
    {
        var group = new OptionGroup
        {
            Id = "genies", Kind = "upgrade", Select = "count", Raw = "up to two Genies",
            Choices = [new OptionChoice { Id = "genies", Name = "Genies", Points = 85, Max = 2, Raw = "Genies" }],
        };
        var unit = TestHelpers.MakeUnit(name: "Magus", points: 100, troopType: "Infantry (Character)", options: [group]);
        var original = new UnitConfiguration(unit);
        original.Selections.Set("genies", "genies", 2);

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var result = await serializer.DeserializeAsync(serializer.Serialize(DwarfList(original)));

        Assert.Equal(2, result.List!.Units.Single().Selections.CountOf("genies", "genies"));
    }

    [Fact]
    public async Task TheFieldedProfile_RoundTrips()
    {
        var general = new ProfileEntry { Id = "general", Name = "General", Points = 95 };
        var captain = new ProfileEntry { Id = "captain", Name = "Captain", Points = 55 };
        var unit = TestHelpers.MakeUnit(name: "Commanders", troopType: "Infantry (Character)", profiles: [general, captain]);
        var original = new UnitConfiguration(unit) { SelectedProfileId = "captain" };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(DwarfList(original));
        Assert.Contains("\"profileId\": \"captain\"", json);

        var result = await serializer.DeserializeAsync(json);
        Assert.Equal("captain", result.List!.Units.Single().SelectedProfileId);
    }

    [Fact]
    public void AnEntryWithOneProfile_WritesNoProfileId()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService())
            .Serialize(DwarfList(new UnitConfiguration(unit)));

        Assert.DoesNotContain("profileId", json);
    }

    // ─── Round trips ────────────────────────────────────────────────

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
            FactionId = "dwarfs",
            PointsTarget = 1500,
            Name = "Roundtrip Army",
            Units = { original },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(list);
        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Equal("dwarfs", result.List!.FactionId);
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
        var json = """{"schema":"army-list/v2","faction":"not-a-faction","pointsTarget":2000,"name":"x","units":[]}""";

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
          "schema": "army-list/v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Test",
          "units": [
            { "unitId": "warriors", "unitName": "Warriors", "modelCount": 10 },
            { "unitId": "ghost-unit", "unitName": "Ghost Unit", "modelCount": 1 }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Single(result.List!.Units);
        Assert.Equal("Warriors", result.List.Units[0].ResolvedUnit.Name);
        Assert.Contains(result.Warnings, w => w.Contains("Ghost Unit"));
    }

    /// <summary>A later release may rename an id; the saved name is the fallback, with a warning.</summary>
    [Fact]
    public async Task DeserializeAsync_UnknownId_FallsBackToTheName()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {
          "schema": "army-list/v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Test",
          "units": [ { "unitId": "dwarf-warriors", "unitName": "Warriors", "modelCount": 10 } ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Equal("Warriors", Assert.Single(result.List!.Units).ResolvedUnit.Name);
        Assert.Contains(result.Warnings, w => w.Contains("matched by name"));
    }

    [Fact]
    public async Task DeserializeAsync_SchemaMismatch_WarnsButContinues()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {"schema":"wap-army-list/v1","faction":"dwarfs","pointsTarget":1000,"name":"Old","units":[]}
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Schema"));
    }

    [Fact]
    public void Serialize_IncludesCurrentSystem()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService())
            .Serialize(DwarfList(new UnitConfiguration(unit)));

        Assert.Contains($"\"system\": \"{GameSystems.Default}\"", json);
    }

    [Fact]
    public async Task DeserializeAsync_DifferentSystem_WarnsButContinues()
    {
        var serializer = new ArmyListSerializer(new StubRepo(), new GameSystemService());
        var json = """{"schema":"army-list/v2","system":"old-world","faction":"dwarfs","pointsTarget":2000,"name":"X","units":[]}""";

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("saved for system 'old-world'"));
    }

    // ─── Faction upgrades ───────────────────────────────────────────

    [Fact]
    public async Task DeserializeAsync_FactionUpgrades_ResolvesCostsFromTheBook()
    {
        // Saved lists store entry ids only; costs are re-resolved from the book on import.
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var repo = new StubRepo(warriors)
        {
            Upgrades =
            [
                new UpgradeEntry { Id = "blessed-spawning-of-chotec", Chapter = "BLESSED SPAWNINGS", Name = "BLESSED SPAWNING OF CHOTEC", Cost = new UpgradeCost { Flat = 10 } },
                new UpgradeEntry { Id = "blessed-spawning-of-itzl", Chapter = "BLESSED SPAWNINGS", Name = "BLESSED SPAWNING OF ITZL", Cost = new UpgradeCost { Flat = 5 } },
            ],
        };
        var serializer = new ArmyListSerializer(repo, new GameSystemService());
        var json = """
        {
          "schema": "army-list/v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy",
          "units": [
            {
              "unitId": "warriors",
              "unitName": "Warriors",
              "modelCount": 10,
              "factionUpgrades": { "blessed-spawnings": ["blessed-spawning-of-itzl"] }
            }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var picks = result.List!.Units[0].Loadout.SelectedFactionUpgrades["blessed-spawnings"];
        var pick = Assert.Single(picks);
        Assert.Equal("blessed-spawning-of-itzl", pick.Id);
        Assert.Equal("BLESSED SPAWNING OF ITZL", pick.Name);
        Assert.Equal(5, pick.PointsCost);
    }

    [Fact]
    public async Task DeserializeAsync_FactionUpgrades_TieredCost_IsReadForTheUnit()
    {
        // Knightly Orders: free for characters, 1 pt/model for cavalry and infantry.
        var knights = TestHelpers.MakeUnit(name: "Knights", points: 20, troopType: "Cavalry");
        var repo = new StubRepo(knights)
        {
            Upgrades =
            [
                new UpgradeEntry
                {
                    Id = "reiksguard-knights", Chapter = "KNIGHTLY ORDERS", Name = "REIKSGUARD KNIGHTS",
                    Cost = new UpgradeCost
                    {
                        Tiers =
                        [
                            new UpgradeCostTier { Scope = "Characters", Text = "free" },
                            new UpgradeCostTier { Scope = "Cavalry/Infantry", Text = "1 point per model" },
                        ],
                    },
                },
            ],
        };
        var serializer = new ArmyListSerializer(repo, new GameSystemService());
        var json = """
        {
          "schema": "army-list/v2", "faction": "dwarfs", "pointsTarget": 2000, "name": "x",
          "units": [ { "unitId": "knights", "unitName": "Knights", "modelCount": 10,
                       "factionUpgrades": { "knightly-orders": ["reiksguard-knights"] } } ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        var pick = Assert.Single(result.List!.Units[0].Loadout.SelectedFactionUpgrades["knightly-orders"]);
        Assert.Equal(10, pick.PointsCost);   // 1 pt/model × 10 models
    }

    [Fact]
    public async Task DeserializeAsync_FactionUpgrades_UnknownId_ImportsAtZeroCost()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = """
        {
          "schema": "army-list/v2",
          "faction": "dwarfs",
          "pointsTarget": 2000,
          "name": "Legacy",
          "units": [
            {
              "unitId": "warriors",
              "unitName": "Warriors",
              "modelCount": 10,
              "factionUpgrades": { "blessed-spawnings": ["retired-spawning"] }
            }
          ]
        }
        """;

        var result = await serializer.DeserializeAsync(json);

        Assert.True(result.Success);
        var picks = result.List!.Units[0].Loadout.SelectedFactionUpgrades["blessed-spawnings"];
        Assert.Equal(0, Assert.Single(picks).PointsCost);
    }

    [Fact]
    public void Serialize_FactionUpgrades_WritesIdsOnly()
    {
        var unit = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Loadout.SelectedFactionUpgrades["blessed-spawnings"] =
        [
            new SelectedFactionUpgrade("blessed-spawning-of-chotec", "Blessed Spawning Of Chotec", 10),
        ];

        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService()).Serialize(DwarfList(config));

        Assert.Contains("\"blessed-spawning-of-chotec\"", json);
        Assert.DoesNotContain("Blessed Spawning Of Chotec", json);
        Assert.DoesNotContain("pointsCost", json);
    }

    // ─── Names and items ────────────────────────────────────────────

    [Fact]
    public async Task DeserializeAsync_RoundTripsCustomName()
    {
        var unit = TestHelpers.MakeUnit(name: "Empire Captain", points: 65);
        var config = new UnitConfiguration(unit)
        {
            ModelCount = 1,
            Loadout = { CustomName = "Aldric the Bold" },
        };

        var serializer = new ArmyListSerializer(new StubRepo(unit), new GameSystemService());
        var json = serializer.Serialize(DwarfList(config));
        Assert.Contains("Aldric the Bold", json);

        var result = await serializer.DeserializeAsync(json);
        Assert.True(result.Success);
        Assert.Equal("Aldric the Bold", result.List!.Units[0].Loadout.CustomName);
    }

    [Fact]
    public void Serialize_OmitsCustomNameWhenNullOrBlank()
    {
        var unit = TestHelpers.MakeUnit(name: "Empire Captain", points: 65);
        var json = new ArmyListSerializer(new StubRepo(unit), new GameSystemService())
            .Serialize(DwarfList(new UnitConfiguration(unit) { Loadout = { CustomName = "   " } }));

        Assert.DoesNotContain("customName", json);
    }

    [Fact]
    public async Task Serialize_IncludesBannerAndItemsWithTheirIds()
    {
        var warriors = TestHelpers.MakeUnit(name: "Warriors", points: 8);
        var config = new UnitConfiguration(warriors)
        {
            ModelCount = 20,
            Loadout =
            {
                SelectedBanner = new SelectedMagicItem
                {
                    Id = "razor-standard",
                    Name = "Razor Standard",
                    Category = MagicItemCategories.Standard,
                    PointsCost = 45,
                },
            },
        };
        config.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Id = "giant-blade", Name = "Giant Blade", Category = MagicItemCategories.Weapon, PointsCost = 45,
        });

        var serializer = new ArmyListSerializer(new StubRepo(warriors), new GameSystemService());
        var json = serializer.Serialize(DwarfList(config));

        Assert.Contains("Razor Standard", json);
        Assert.Contains("\"id\": \"razor-standard\"", json);
        Assert.Contains("\"id\": \"giant-blade\"", json);

        var result = await serializer.DeserializeAsync(json);
        var copy = result.List!.Units.Single();
        Assert.Equal("razor-standard", copy.Loadout.SelectedBanner!.Id);
        Assert.Equal("giant-blade", Assert.Single(copy.SelectedMagicItems).Id);
    }
}
