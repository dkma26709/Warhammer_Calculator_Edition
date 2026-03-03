using Warhammer.Services;

namespace Warhammer.Tests;

public class EquipmentParserTests
{
    [Fact]
    public void Parse_HandWeaponLightArmour()
    {
        var (weapons, armour) = EquipmentParser.Parse("Hand weapon, light armour");
        Assert.Contains("handWeapon", weapons);
        Assert.Contains("lightArmour", armour);
    }

    [Fact]
    public void Parse_HandWeaponShield()
    {
        var (weapons, armour) = EquipmentParser.Parse("Hand weapon, shield");
        Assert.Contains("handWeapon", weapons);
        Assert.Contains("shields", armour);
    }

    [Fact]
    public void Parse_SpearShield()
    {
        var (weapons, armour) = EquipmentParser.Parse("Spear, shield");
        Assert.Contains("spear", weapons);
        Assert.Contains("shields", armour);
    }

    [Fact]
    public void Parse_HeavyArmourShieldBarding()
    {
        var (weapons, armour) = EquipmentParser.Parse("Hand weapon, heavy armour, shield, barding");
        Assert.Contains("handWeapon", weapons);
        Assert.Contains("heavyArmour", armour);
        Assert.Contains("shields", armour);
        Assert.Contains("barding", armour);
    }

    [Fact]
    public void Parse_Null_ReturnsHandWeaponDefault()
    {
        var (weapons, armour) = EquipmentParser.Parse(null);
        Assert.Empty(weapons);
        Assert.Empty(armour);
    }

    [Fact]
    public void Parse_NewlineTruncation()
    {
        var (weapons, armour) = EquipmentParser.Parse("Hand weapon\nMagic item stuff");
        Assert.Contains("handWeapon", weapons);
        Assert.Empty(armour);
    }

    [Fact]
    public void Parse_GreatWeapon()
    {
        var (weapons, _) = EquipmentParser.Parse("Great weapon");
        Assert.Contains("greatWeapon", weapons);
    }

    [Fact]
    public void Parse_TwoHandWeapons()
    {
        var (weapons, _) = EquipmentParser.Parse("Two hand weapons");
        Assert.Contains("two_additionalHandWeapons", weapons);
    }

    [Fact]
    public void Parse_Flail()
    {
        var (weapons, _) = EquipmentParser.Parse("Flail");
        Assert.Contains("flail", weapons);
    }

    [Fact]
    public void Parse_MediumArmour()
    {
        var (_, armour) = EquipmentParser.Parse("Hand weapon, medium armour");
        Assert.Contains("mediumArmour", armour);
    }

    [Fact]
    public void Parse_Buckler()
    {
        var (_, armour) = EquipmentParser.Parse("Hand weapon, buckler");
        Assert.Contains("buckler", armour);
    }

    [Fact]
    public void Parse_Polearm()
    {
        var (weapons, _) = EquipmentParser.Parse("Polearm");
        Assert.Contains("polearm", weapons);
    }

    [Fact]
    public void Parse_Plurals_Handled()
    {
        var (weapons, armour) = EquipmentParser.Parse("Spears, shields");
        Assert.Contains("spear", weapons);
        Assert.Contains("shields", armour);
    }
}
