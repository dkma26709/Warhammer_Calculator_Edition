using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// The Warhammer Armies Revamped data folder, wwwroot/data/war/: exported by export.py in the
/// warhammer-armies-revamped repo, at the commit in tools/war-data/pin, by
/// tools/war-data/fetch.py, all but core-rules.json and troop-types.json, which are
/// hand-maintained here. The exported files are generated, never hand-edited; these tests are
/// the guard against a half-copied folder. The tests that run over
/// every enabled system check what is in the files.
/// </summary>
public class WarDataTests
{
    /// <summary>The army books the edition has. Update when a book is added or removed.</summary>
    private const int FactionCount = 18;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string DataDir => TestPaths.SystemDataDir(GameSystems.War);

    private static SystemManifest Manifest() =>
        JsonSerializer.Deserialize<SystemManifest>(
            File.ReadAllText(Path.Combine(DataDir, "manifest.json")), JsonOptions)!;

    [Fact]
    public void Manifest_ListsEveryArmy()
    {
        var manifest = Manifest();
        Assert.Equal(GameSystems.War, manifest.System);
        Assert.Equal(FactionCount, manifest.Factions.Count);
    }

    /// <summary>
    /// fetch.py clears the folder before it copies the export in, but a file put here any other
    /// way — copied by hand, say an army since renamed, or the one-file war.json this folder used
    /// to hold — would sit here looking current while nothing reads it.
    /// </summary>
    [Fact]
    public void TheFolderHoldsExactlyTheFilesTheSystemReads()
    {
        var expected = new List<string>
        {
            "common-magic-items.json", "composition.json", "core-rules.json", "manifest.json",
            "troop-types.json",
        };
        foreach (var faction in Manifest().Factions)
        {
            expected.Add($"factions/{faction}.json");
            expected.Add($"factions/{faction}-extras.json");
        }

        var actual = Directory.EnumerateFiles(DataDir, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(DataDir, path).Replace('\\', '/'));

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }
}
