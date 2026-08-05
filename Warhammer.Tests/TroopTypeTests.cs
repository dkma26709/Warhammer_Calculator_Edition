using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class TroopTypeTests
{
    [Fact]
    public void Parse_Infantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Infantry");
        Assert.Equal(BaseTroopType.Infantry, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
        Assert.Equal(1, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
        Assert.False(info.IsCharacter);
    }

    [Fact]
    public void Parse_LargeInfantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Large Infantry");
        Assert.Equal(BaseTroopType.LargeInfantry, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(4, info.MinRankWidth);
        Assert.Equal(2, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_MonstrousInfantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monstrous Infantry");
        Assert.Equal(BaseTroopType.MonstrousInfantry, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        Assert.Equal(2, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_Cavalry_ReturnsSplitProfile()
    {
        var info = TroopTypeInfo.Parse("Cavalry");
        Assert.Equal(BaseTroopType.Cavalry, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
        Assert.True(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_MonstrousCavalry_ReturnsSplitProfile()
    {
        var info = TroopTypeInfo.Parse("Monstrous Cavalry");
        Assert.Equal(BaseTroopType.MonstrousCavalry, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        // "one supporting attack from the rider, and two from the mount" — both books.
        // This asserted 2 for the rider, which with the mount's 2 gave four attacks, not three.
        Assert.Equal(1, info.MaxSupportingAttacks);
        Assert.Equal(2, info.MountSupportingAttacks);
        Assert.Equal(3, info.NonSplitSupportingAttacks);
        Assert.True(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_WarBeasts_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("War Beasts");
        Assert.Equal(BaseTroopType.WarBeasts, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
    }

    [Fact]
    public void Parse_MonstrousBeast_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monstrous Beast");
        Assert.Equal(BaseTroopType.MonstrousBeasts, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
    }

    [Fact]
    public void Parse_Swarms_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Swarms");
        Assert.Equal(BaseTroopType.Swarms, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        Assert.Equal(5, info.MaxSupportingAttacks);
    }

    [Fact]
    public void Parse_Monster_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monster");
        Assert.Equal(BaseTroopType.Monster, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(1, info.MinRankWidth);
        Assert.Equal(0, info.MaxSupportingAttacks);
    }

    [Fact]
    public void Parse_Chariot_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Chariot");
        Assert.Equal(BaseTroopType.Chariot, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
    }

    [Fact]
    public void Parse_Shrine_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Shrine");
        Assert.Equal(BaseTroopType.Shrine, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
    }

    [Fact]
    public void Parse_Typo_MosntrousBeast_IsCorrected()
    {
        var info = TroopTypeInfo.Parse("Mosntrous Beast");
        Assert.Equal(BaseTroopType.MonstrousBeasts, info.BaseType);
    }

    [Fact]
    public void Parse_CharacterFlag_SetsIsCharacter()
    {
        var info = TroopTypeInfo.Parse("Infantry (Character, Human)");
        Assert.Equal(BaseTroopType.Infantry, info.BaseType);
        Assert.True(info.IsCharacter);
    }

    [Fact]
    public void Parse_ArmourSaveFlag_SetsIntrinsicArmour()
    {
        var info = TroopTypeInfo.Parse("Chariot (Armour Save 4+)");
        Assert.Equal(BaseTroopType.Chariot, info.BaseType);
        Assert.Equal(4, info.IntrinsicArmourSave);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsDefaultInfantry()
    {
        var info1 = TroopTypeInfo.Parse(null);
        Assert.Equal(BaseTroopType.Infantry, info1.BaseType);

        var info2 = TroopTypeInfo.Parse("");
        Assert.Equal(BaseTroopType.Infantry, info2.BaseType);

        var info3 = TroopTypeInfo.Parse("  ");
        Assert.Equal(BaseTroopType.Infantry, info3.BaseType);
    }

    [Fact]
    public void Parse_WarMachine_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("War Machine");
        Assert.Equal(BaseTroopType.WarMachine, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(1, info.MinRankWidth);
    }
}
