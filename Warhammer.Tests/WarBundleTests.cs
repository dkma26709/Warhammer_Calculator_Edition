using System.Text.Json;
using System.Text.RegularExpressions;

namespace Warhammer.Tests;

/// <summary>
/// The Warhammer Armies Revamped bundle: one file, wwwroot/data/war/war.json,
/// exported by export.py in the warhammer-armies-revamped repo and copied here
/// on a release. It is generated, never hand-edited; these tests are the guard
/// against a half-copied or edited file, not a schema check.
/// </summary>
public class WarBundleTests
{
    /// <summary>The army books the edition has. Update when a book is added.</summary>
    private const int FactionCount = 30;

    private static readonly Lazy<JsonDocument> Bundle = new(() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TestPaths.SystemDataDir("war"), "war.json"))));

    [Fact]
    public void Bundle_NamesTheCommitItCameFrom()
    {
        var root = Bundle.Value.RootElement;
        Assert.Equal("war", root.GetProperty("system").GetString());
        var source = root.GetProperty("source").GetString();
        Assert.Matches(new Regex("^[0-9a-f]{40}$"), source);
    }

    [Fact]
    public void Bundle_HasEveryFaction()
    {
        var factions = Bundle.Value.RootElement.GetProperty("factions");
        Assert.Equal(FactionCount, factions.EnumerateObject().Count());
        foreach (var faction in factions.EnumerateObject())
        {
            Assert.False(string.IsNullOrEmpty(faction.Value.GetProperty("name").GetString()),
                $"{faction.Name} has no name");
            Assert.True(faction.Value.GetProperty("units").GetArrayLength() > 0,
                $"{faction.Name} has no units");
        }
    }

    [Fact]
    public void Bundle_HasTheRulebook()
    {
        var rulebook = Bundle.Value.RootElement.GetProperty("rulebook");
        Assert.True(rulebook.GetProperty("rules").EnumerateObject().Any(), "no special rules");
        Assert.True(rulebook.GetProperty("items").GetArrayLength() > 0, "no common magic items");
        Assert.True(rulebook.GetProperty("lores").GetArrayLength() > 0, "no lores");
    }
}
