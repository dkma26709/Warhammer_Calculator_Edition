using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

public class UpgradePriceTests
{
    private const string Reiksguard =
        "Characters free\nCavalry/Infantry 1 point per model\nMonstrous Cavalry 3 points per model";

    private static readonly TroopTypeInfo Infantry = new() { BaseType = BaseTroopType.Infantry };
    private static readonly TroopTypeInfo Cavalry = new() { BaseType = BaseTroopType.Cavalry };
    private static readonly TroopTypeInfo MonstrousCavalry = new() { BaseType = BaseTroopType.MonstrousCavalry };
    private static readonly TroopTypeInfo MonstrousInfantry = new() { BaseType = BaseTroopType.MonstrousInfantry };
    private static readonly TroopTypeInfo Monster = new() { BaseType = BaseTroopType.Monster };
    private static readonly TroopTypeInfo Character = new() { BaseType = BaseTroopType.Infantry, IsCharacter = true };

    [Theory]
    [InlineData("10", 10)]
    [InlineData("+5", 5)]
    [InlineData("2.5", 2.5)]
    public void PlainNumber_IsAFlatPriceForEveryUnit(string text, double expected)
    {
        Assert.Equal(new UpgradePrice(expected, false), UpgradePrice.Resolve(text, Cavalry, UnitCategory.Core));
        Assert.Equal(new UpgradePrice(expected, false), UpgradePrice.Resolve(text, Character, UnitCategory.Characters));
        Assert.True(UpgradePrice.IsRecognised(text));
        Assert.False(UpgradePrice.IsTiered(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_IsFree(string? text)
    {
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(text, Infantry, UnitCategory.Core));
        Assert.True(UpgradePrice.IsRecognised(text));
        Assert.False(UpgradePrice.IsTiered(text));
    }

    [Fact]
    public void Tiered_FreeLine_IsFree()
    {
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(Reiksguard, Character, UnitCategory.Characters));
        Assert.True(UpgradePrice.IsTiered(Reiksguard));
    }

    [Fact]
    public void Tiered_CharacterLine_IsFlat()
    {
        const string text = "Characters 10 points\nCavalry/Infantry 1 point per model\nMonstrous Cavalry free";
        Assert.Equal(new UpgradePrice(10, false), UpgradePrice.Resolve(text, Character, UnitCategory.Characters));
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(text, MonstrousCavalry, UnitCategory.Rare));
    }

    [Fact]
    public void Tiered_PerModelLine_IsPerModel()
    {
        Assert.Equal(new UpgradePrice(3, true), UpgradePrice.Resolve(Reiksguard, MonstrousCavalry, UnitCategory.Rare));
    }

    [Fact]
    public void Tiered_DecimalPerModel_Parses()
    {
        const string text = "Characters 5 points\nInfantry 0.5 point per model\nMonstrous Infantry 1.5 points per model";
        Assert.Equal(new UpgradePrice(0.5, true), UpgradePrice.Resolve(text, Infantry, UnitCategory.Core));
        Assert.Equal(new UpgradePrice(1.5, true), UpgradePrice.Resolve(text, MonstrousInfantry, UnitCategory.Special));
    }

    [Fact]
    public void Tiered_SlashSeparatedTiers_EachTakeTheLine()
    {
        Assert.Equal(new UpgradePrice(1, true), UpgradePrice.Resolve(Reiksguard, Cavalry, UnitCategory.Special));
        Assert.Equal(new UpgradePrice(1, true), UpgradePrice.Resolve(Reiksguard, Infantry, UnitCategory.Special));
    }

    [Fact]
    public void Tiered_PluralPointsPerModel_AndMixedCase_Parse()
    {
        // Dogs of War writes "1 points per model"; tier names and "free" vary in case.
        const string text = "characters FREE\nCAVALRY 1 points Per Model";
        Assert.Equal(new UpgradePrice(1, true), UpgradePrice.Resolve(text, Cavalry, UnitCategory.Core));
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(text, Character, UnitCategory.Characters));
    }

    [Fact]
    public void Tiered_NoLineForTheUnitsTier_IsNotOffered()
    {
        const string panther = "Characters free\nCavalry 1 point per model";
        Assert.Null(UpgradePrice.Resolve(panther, Infantry, UnitCategory.Special));
        Assert.Null(UpgradePrice.Resolve(panther, Monster, UnitCategory.Rare));
        Assert.Null(UpgradePrice.Resolve("Characters 25 points", Cavalry, UnitCategory.Core));
    }

    [Fact]
    public void Tiered_CharacterOnAnyBase_TakesTheCharactersLine()
    {
        var cavalryCharacter = new TroopTypeInfo { BaseType = BaseTroopType.Cavalry, IsCharacter = true };
        var ogreCharacter = new TroopTypeInfo { BaseType = BaseTroopType.MonstrousInfantry, IsCharacter = true };
        const string text = "Characters 15 points\nInfantry/Cavalry 1 point per model\nMonstrous Infantry 3 points per model";

        Assert.Equal(new UpgradePrice(15, false), UpgradePrice.Resolve(text, cavalryCharacter, UnitCategory.Characters));
        Assert.Equal(new UpgradePrice(15, false), UpgradePrice.Resolve(text, ogreCharacter, UnitCategory.Characters));
        // The category alone marks a character too, whatever its troop type string says.
        Assert.Equal(new UpgradePrice(15, false), UpgradePrice.Resolve(text, MonstrousCavalry, UnitCategory.Lords));
    }

    [Fact]
    public void Tiered_CharactersLine_IsFlatEvenWhenWrittenPerModel()
    {
        Assert.Equal(new UpgradePrice(2, false),
            UpgradePrice.Resolve("Characters 2 points per model", Character, UnitCategory.Characters));
    }

    [Theory]
    [InlineData("25/50/")]
    [InlineData("15/25 points")]
    [InlineData("Characters 10 points\nDragons 5 points")]
    [InlineData("Characters 10 points\nsee below")]
    public void UnrecognisedText_IsFree_AndNotRecognised(string text)
    {
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(text, Cavalry, UnitCategory.Core));
        Assert.Equal(new UpgradePrice(0, false), UpgradePrice.Resolve(text, Character, UnitCategory.Characters));
        Assert.False(UpgradePrice.IsRecognised(text));
        Assert.False(UpgradePrice.IsTiered(text));
    }

    // ─── Cumulative runes ("15/25/50") ───────────────────────────

    [Theory]
    [InlineData("15/25/50", 0, 0)]
    [InlineData("15/25/50", 1, 15)]
    [InlineData("15/25/50", 2, 40)]
    [InlineData("15/25/50", 3, 90)]
    [InlineData("25/35", 2, 60)]
    [InlineData("25/35", 3, 60)]
    [InlineData("25/35", -1, 0)]
    [InlineData("2.5/7.5", 2, 10)]
    public void CumulativeTotal_SumsTheFirstCopiesFigures_ClampedToTheFigureCount(string text, int copies, double expected)
    {
        Assert.Equal(expected, UpgradePrice.CumulativeTotal(text, copies));
    }

    [Fact]
    public void Cumulative_IsRecognised_AndResolvesToOneCopysFlatPrice()
    {
        Assert.True(UpgradePrice.IsRecognised("5/35/55"));
        Assert.True(UpgradePrice.IsCumulative("5/35/55"));
        Assert.False(UpgradePrice.IsTiered("5/35/55"));
        Assert.Equal(new UpgradePrice(15, false), UpgradePrice.Resolve("15/25/50", Character, UnitCategory.Characters));
        Assert.Equal(new UpgradePrice(15, false), UpgradePrice.Resolve("15/25/50", Cavalry, UnitCategory.Core));
    }

    [Fact]
    public void Cumulative_ParsesEveryFigure_IncludingDecimals()
    {
        Assert.True(UpgradePrice.TryParseCumulative("15/25/50", out var whole));
        Assert.Equal([15d, 25d, 50d], whole);
        Assert.True(UpgradePrice.TryParseCumulative("0.5/1.5", out var halves));
        Assert.Equal([0.5, 1.5], halves);
        Assert.Equal(3, UpgradePrice.MaxCopies("15/25/50"));
        Assert.Equal(2, UpgradePrice.MaxCopies("0.5/1.5"));
    }

    [Theory]
    [InlineData("15")]
    [InlineData("2.5")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Characters 10 points")]
    public void NonCumulativeText_IsNotCumulative_AndAllowsOneCopy(string? text)
    {
        Assert.False(UpgradePrice.IsCumulative(text));
        Assert.False(UpgradePrice.TryParseCumulative(text, out var perCopy));
        Assert.Empty(perCopy);
        Assert.Equal(1, UpgradePrice.MaxCopies(text));
    }

    [Fact]
    public void CumulativeTotal_PlainNumber_IsOneFigure_AndUnparsedTextIsFree()
    {
        Assert.Equal(35, UpgradePrice.CumulativeTotal("35", 1));
        Assert.Equal(35, UpgradePrice.CumulativeTotal("35", 2));
        Assert.Equal(0, UpgradePrice.CumulativeTotal("Characters 10 points", 1));
        Assert.Equal(0, UpgradePrice.CumulativeTotal(null, 1));
    }

    [Fact]
    public void Tiered_BlankLinesAndCarriageReturns_AreIgnored()
    {
        const string text = "Characters 10 points\r\n\r\nCavalry 2 points per model\r\n";
        Assert.True(UpgradePrice.IsRecognised(text));
        Assert.Equal(new UpgradePrice(2, true), UpgradePrice.Resolve(text, Cavalry, UnitCategory.Core));
    }

    [Theory]
    [InlineData(0, false, "free")]
    [InlineData(10, false, "10 pts")]
    [InlineData(1, false, "1 pt")]
    [InlineData(1, true, "1 pt/model")]
    [InlineData(3, true, "3 pt/model")]
    [InlineData(0.5, true, "0.5 pt/model")]
    [InlineData(1.5, true, "1.5 pt/model")]
    public void Label_FormatsWithoutTrailingZeros(double points, bool perModel, string expected)
    {
        Assert.Equal(expected, new UpgradePrice(points, perModel).Label);
    }
}
