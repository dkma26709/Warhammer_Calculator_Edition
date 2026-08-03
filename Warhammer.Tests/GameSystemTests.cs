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

    [Fact]
    public void Initialize_EnabledKey_SetsCurrentSystem()
    {
        var service = new GameSystemService();
        service.Initialize("wap-v2");
        Assert.Equal("wap-v2", service.CurrentSystem);
    }

    [Fact]
    public void DataPath_ResolvesInsideSystemFolder()
    {
        var service = new GameSystemService();
        Assert.Equal("data/wap-v2/core-rules.json", service.DataPath("core-rules.json"));
        Assert.Equal("data/wap-v2/factions/dwarfs.json", service.DataPath("factions/dwarfs.json"));
    }

    [Fact]
    public void Manifest_ParsesAndEveryFactionResolvesWithDataFiles()
    {
        var systemDir = Path.Combine(
            TestHelpers.FindProjectRoot(), "wwwroot", "data", GameSystems.Default);
        var manifest = JsonSerializer.Deserialize<SystemManifest>(
            File.ReadAllText(Path.Combine(systemDir, "manifest.json")), JsonOptions);

        Assert.NotNull(manifest);
        Assert.Equal(GameSystems.Default, manifest.System);
        Assert.NotEmpty(manifest.Factions);

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

    [Fact]
    public void Repository_AllFactions_ComesFromManifest()
    {
        // TestHelpers.Repo loads the wap-v2 manifest from disk; it lists all 15 factions.
        Assert.Equal(FactionKeyExtensions.All, TestHelpers.Repo.AllFactions);
    }
}
