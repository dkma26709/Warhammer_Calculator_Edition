using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The army books put special characters under their own SPECIAL CHARACTERS heading, which the
/// extractor files as category "Named Characters". UnitCategoryExtensions had no key for it, so
/// TryFromJsonKey failed, ResolveUnit returned null and GetUnitsAsync dropped the unit — silently,
/// because the caller only adds non-null. That hid 253 of 920 wap-v2 units and 205 of 902 wap-v3
/// units: every named character in the game.
/// </summary>
public class NamedCharacterCategoryTests
{
    [Fact]
    public void NamedCharactersMapFromTheirJsonKey()
    {
        Assert.True(UnitCategoryExtensions.TryFromJsonKey("Named Characters", out var category));
        Assert.Equal(UnitCategory.NamedCharacters, category);
        Assert.Equal("Named Characters", UnitCategory.NamedCharacters.ToJsonKey());
    }

    /// <summary>
    /// They are characters for every purpose the engine cares about: the combined characters cap,
    /// the single-character cap, and standing as the army's General.
    /// </summary>
    [Fact]
    public void NamedCharactersCountAsCharacters()
    {
        Assert.True(UnitCategory.NamedCharacters.IsCharacterCategory());
        Assert.Contains(UnitCategory.NamedCharacters, UnitCategoryExtensions.CharacterCategories);
    }

    /// <summary>
    /// The combined cap and the Builder's summary both sum this list, so a category that holds
    /// characters can never be counted by one and missed by the other.
    /// </summary>
    [Fact]
    public void EveryCharacterCategoryIsInTheCharacterCategoryList()
    {
        foreach (var category in Enum.GetValues<UnitCategory>())
        {
            Assert.Equal(
                category.IsCharacterCategory(),
                UnitCategoryExtensions.CharacterCategories.Contains(category));
        }
    }

    [Theory]
    [InlineData(GameSystems.WapV2, FactionKey.Dwarfs, "Thorgrim Grudgebearer")]
    [InlineData(GameSystems.WapV3, FactionKey.Dwarfs, "Thorgrim Grudgebearer")]
    [InlineData(GameSystems.WapV3, FactionKey.HighElves, "Caradryan of the Flame")]
    [InlineData(GameSystems.WapV3, FactionKey.Skaven, "Nurglitch")]
    public async Task NamedCharactersNowResolve(string system, FactionKey faction, string name)
    {
        var units = await TestHelpers.RepoFor(system).GetUnitsAsync(faction);
        var unit = units.FirstOrDefault(u => u.Name == name);

        Assert.NotNull(unit);
        Assert.Equal(UnitCategory.NamedCharacters, unit.Category);
    }

    /// <summary>
    /// A category present in the shipped data but absent from the mapping is dropped without a
    /// word, so this compares what the repository returns against what the JSON holds — for every
    /// faction at once, rather than waiting for someone to notice a missing unit.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    [InlineData(GameSystems.War)]
    public async Task EveryFactionResolvesEveryUnitInItsData(string system)
    {
        var repo = TestHelpers.RepoFor(system);
        var factionsDir = Path.Combine(
            TestHelpers.FindProjectRoot(), "wwwroot", "data", system, "factions");
        var shortfalls = new List<string>();

        foreach (var faction in Enum.GetValues<FactionKey>())
        {
            var path = Path.Combine(factionsDir, $"{faction.ToJsonKey()}.json");
            if (!File.Exists(path))
                continue;

            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("units", out var unitsNode))
                continue;

            int inData = unitsNode.GetArrayLength();
            int resolved = (await repo.GetUnitsAsync(faction)).Count;

            if (resolved != inData)
                shortfalls.Add($"{faction}: repository returned {resolved} of {inData}");
        }

        Assert.Empty(shortfalls);
    }
}
