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

        public Task EnsureLoadedAsync() => Task.CompletedTask;
        public IReadOnlyList<FactionKey> AllFactions => [];
        public string GetFactionDisplayName(FactionKey faction) => faction.ToString();
        public Task<IReadOnlyList<Unit>> GetUnitsAsync(FactionKey faction) => Task.FromResult(_units);
        public Task<FactionExtrasData> GetFactionExtrasAsync(FactionKey faction) =>
            Task.FromResult(new FactionExtrasData());
        public Task<IReadOnlyList<MagicItemEntry>> GetCommonMagicItemsAsync() =>
            Task.FromResult<IReadOnlyList<MagicItemEntry>>([]);
        public Weapon? GetWeapon(CloseCombatWeaponKey key) => null;
        public Weapon GetHandWeapon() => throw new NotImplementedException();
        public Armour? GetArmour(ArmourKey key) => null;
        public int GetToHitScore(int attackerWS, int defenderWS) => 4;
        public int GetToWoundScore(int strength, int toughness) => 4;
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
