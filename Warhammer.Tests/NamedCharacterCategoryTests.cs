using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

/// <summary>
/// The army books put special characters under their own SPECIAL CHARACTERS heading, which the
/// bundle marks <c>named</c>. They are characters for every purpose the engine cares about:
/// the combined characters cap, the single-unit cap, and standing as the army's General — and
/// each may be fielded once.
/// </summary>
public class NamedCharacterCategoryTests
{
    [Fact]
    public void NamedCharactersMapFromTheirSection()
    {
        Assert.Equal(UnitCategory.NamedCharacters, UnitCategoryExtensions.FromSection("characters", null, named: true));
        Assert.Equal("Named Characters", UnitCategory.NamedCharacters.ToJsonKey());
    }

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
    [InlineData("dwarfs", "THORGRIM GRUDGEBEARER")]
    [InlineData("high-elves", "CARADRYAN OF THE FLAME")]
    [InlineData("skaven", "NURGLITCH")]
    [InlineData("empire", "KARL FRANZ")]
    public async Task NamedCharactersResolveAsNamed(string faction, string name)
    {
        var unit = await TestHelpers.FindUnitAsync(faction, name);

        Assert.Equal(UnitCategory.NamedCharacters, unit.Category);
        Assert.True(unit.Named);
        Assert.True(unit.BaseModel.TroopType.IsCharacter);
    }

    /// <summary>A book's mounts section is kept for what mount options point at, not fielded on its own.</summary>
    [Fact]
    public async Task MountsResolveAsCharacterMounts()
    {
        var warhorse = await TestHelpers.FindUnitAsync("empire", "WARHORSE");

        Assert.Equal(UnitCategory.CharacterMounts, warhorse.Category);
        Assert.Equal(0, warhorse.BaseModelCost);
    }
}
