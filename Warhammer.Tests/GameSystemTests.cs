using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Tests;

public class GameSystemTests
{
    [Fact]
    public void Registry_DefaultSystemIsEnabled()
    {
        Assert.True(GameSystems.IsEnabled(GameSystems.Default));
        Assert.Contains(GameSystems.All, s => s.Key == GameSystems.Default);
    }

    [Fact]
    public void Service_DefaultsToTheDefaultSystem()
    {
        Assert.Equal(GameSystems.Default, new GameSystemService().CurrentSystem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-system")]
    [InlineData("wap-v3")] // an old system this build no longer ships
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
        service.Initialize(GameSystems.War);
        Assert.Equal(GameSystems.War, service.CurrentSystem);
    }

    [Fact]
    public void DataPath_ResolvesInsideSystemFolder()
    {
        var service = new GameSystemService();
        Assert.Equal($"data/{GameSystems.Default}/core-rules.json", service.DataPath("core-rules.json"));
    }

    [Fact]
    public void Bundle_PathIsNamedAfterTheSystem()
    {
        var service = new GameSystemService();
        var bundle = new BundleService(new HttpClient(), service);
        Assert.Equal($"data/{GameSystems.Default}/{GameSystems.Default}.json", bundle.BundlePath);
    }

    public static TheoryData<string> EnabledSystems()
    {
        var data = new TheoryData<string>();
        foreach (var system in GameSystems.All.Where(s => s.Enabled))
            data.Add(system.Key);
        return data;
    }

    /// <summary>Adding a system is a data exercise: the bundle and the two engine files, nothing else.</summary>
    [Theory]
    [MemberData(nameof(EnabledSystems))]
    public void EverySystem_ShipsItsBundleAndEngineFiles(string systemKey)
    {
        var systemDir = TestPaths.SystemDataDir(systemKey);

        Assert.True(File.Exists(Path.Combine(systemDir, $"{systemKey}.json")), $"Missing bundle for '{systemKey}'");
        Assert.True(File.Exists(Path.Combine(systemDir, "core-rules.json")), $"Missing core-rules.json for '{systemKey}'");
        Assert.True(File.Exists(Path.Combine(systemDir, "troop-types.json")), $"Missing troop-types.json for '{systemKey}'");
    }

    [Fact]
    public void Repository_AllFactions_ComesFromTheBundle()
    {
        Assert.Equal(30, TestHelpers.Repo.AllFactions.Count);
        Assert.Contains(TestHelpers.Repo.AllFactions, f => f.Id == "empire" && f.Name == "The Empire");
    }
}
