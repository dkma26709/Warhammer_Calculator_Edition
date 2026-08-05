using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class FactionDeserializationTests
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

    /// <summary>Every (enabled system, manifest faction) pair.</summary>
    public static TheoryData<string, string> SystemFactions()
    {
        var data = new TheoryData<string, string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
        {
            var manifest = JsonSerializer.Deserialize<SystemManifest>(
                File.ReadAllText(Path.Combine(SystemDataDir(system.Key), "manifest.json")), JsonOptions)!;
            foreach (var faction in manifest.Factions)
                data.Add(system.Key, faction);
        }
        return data;
    }

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

    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void FactionUnits_Deserialize(string systemKey, string factionKey)
    {
        var path = Path.Combine(SystemDataDir(systemKey), "factions", $"{factionKey}.json");
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactionData>(json, JsonOptions);
        Assert.NotNull(data);
        Assert.NotEmpty(data.Units);

        // Verify every unit has at least one profile
        foreach (var unit in data.Units)
        {
            Assert.NotEmpty(unit.Profiles);
            Assert.False(string.IsNullOrWhiteSpace(unit.Name), "Unit name should not be empty");
        }
    }

    [Theory]
    [MemberData(nameof(SystemFactions))]
    public void FactionExtras_Deserialize(string systemKey, string factionKey)
    {
        var path = Path.Combine(SystemDataDir(systemKey), "factions", $"{factionKey}-extras.json");
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactionExtrasData>(json, JsonOptions);
        Assert.NotNull(data);
    }
}
