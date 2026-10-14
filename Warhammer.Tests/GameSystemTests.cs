using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class GameSystemTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Registry_DefaultSystemIsEnabled()
    {
        Assert.True(GameSystems.IsEnabled(GameSystems.Default));
        Assert.Contains(GameSystems.All, s => s.Key == GameSystems.Default);
    }

    [Fact]
    public void Service_DefaultsToWapV2()
    {
        Assert.Equal("wap-v2", new GameSystemService().CurrentSystem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-system")]
    [InlineData("old-world")] // exists in the registry but is disabled
    public void Initialize_InvalidOrDisabledKey_KeepsDefault(string? persisted)
    {
        var service = new GameSystemService();
        service.Initialize(persisted);
        Assert.Equal(GameSystems.Default, service.CurrentSystem);
    }

    [Theory]
    [InlineData("wap-v2")]
    [InlineData("wap-v3")]
    public void Initialize_EnabledKey_SetsCurrentSystem(string key)
    {
        var service = new GameSystemService();
        service.Initialize(key);
        Assert.Equal(key, service.CurrentSystem);
    }

    [Fact]
    public void DataPath_ResolvesInsideSystemFolder()
    {
        var service = new GameSystemService();
        Assert.Equal("data/wap-v2/core-rules.json", service.DataPath("core-rules.json"));
        Assert.Equal("data/wap-v2/factions/dwarfs.json", service.DataPath("factions/dwarfs.json"));
    }

    public static TheoryData<string> EnabledSystems()
    {
        var data = new TheoryData<string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
            data.Add(system.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void Manifest_ParsesAndEveryFactionResolvesWithDataFiles(string systemKey)
    {
        var systemDir = Path.Combine(
            TestHelpers.FindProjectRoot(), "wwwroot", "data", systemKey);
        var manifest = JsonSerializer.Deserialize<SystemManifest>(
            File.ReadAllText(Path.Combine(systemDir, "manifest.json")), JsonOptions);

        Assert.NotNull(manifest);
        Assert.Equal(systemKey, manifest.System);
        Assert.NotEmpty(manifest.Factions);

        Assert.True(File.Exists(Path.Combine(systemDir, "core-rules.json")),
            $"Missing core-rules.json for '{systemKey}'");
        Assert.True(File.Exists(Path.Combine(systemDir, "common-magic-items.json")),
            $"Missing common-magic-items.json for '{systemKey}'");

        foreach (var key in manifest.Factions)
        {
            Assert.True(FactionKeyExtensions.TryFromJsonKey(key, out _),
                $"Manifest faction '{key}' has no FactionKey");
            Assert.True(File.Exists(Path.Combine(systemDir, "factions", $"{key}.json")),
                $"Missing roster file for '{key}'");
            Assert.True(File.Exists(Path.Combine(systemDir, "factions", $"{key}-extras.json")),
                $"Missing extras file for '{key}'");
        }
    }

    /// <summary>
    /// The repository offers the active system's manifest, in its order — not every FactionKey,
    /// which spans all systems. A manifest key with no FactionKey is skipped by the loader with
    /// only a console line, so it is asserted here rather than left to go missing from the picker.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public void Repository_AllFactions_ComesFromManifest(string systemKey)
    {
        var manifest = JsonSerializer.Deserialize<SystemManifest>(
            File.ReadAllText(Path.Combine(TestPaths.SystemDataDir(systemKey), "manifest.json")),
            JsonOptions)!;

        var expected = manifest.Factions.Select(jsonKey =>
        {
            Assert.True(FactionKeyExtensions.TryFromJsonKey(jsonKey, out var key),
                $"{systemKey} manifest lists '{jsonKey}', which has no FactionKey");
            return key;
        }).ToList();

        Assert.Equal(expected, TestHelpers.RepoFor(systemKey).AllFactions);
    }
}
