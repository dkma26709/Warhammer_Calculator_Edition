using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class WeaponRulesTests
{
    [Fact]
    public void Parse_GreatWeapon()
    {
        var rules = ParsedWeaponRules.Parse("-2 Initiative, Mighty Blow (1), Requires Two Hands");
        Assert.Equal(-2, rules.InitiativeMod);
        Assert.Equal(1, rules.MightyBlow);
        Assert.True(rules.RequiresTwoHands);
        Assert.Equal(0, rules.ExtraAttacks);
        Assert.Equal(WeaponCondition.Always, rules.MightyBlowCondition);
    }

    [Fact]
    public void Parse_Spear()
    {
        var rules = ParsedWeaponRules.Parse(
            "+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*",
            "Model on foot only. In turns in which the model is charged to its front, it receives an additional +1 to its Initiative.");
        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(1, rules.FightInExtraRanks);
        Assert.Equal(1, rules.MightyBlow);
        Assert.Equal(WeaponCondition.WhenBeingCharged, rules.MightyBlowCondition);
        Assert.Equal(1, rules.ChargedToFrontInitiativeBonus);
    }

    [Fact]
    public void Parse_Flail()
    {
        var rules = ParsedWeaponRules.Parse("Mighty Blow (2), Requires Two Hands");
        Assert.Equal(2, rules.MightyBlow);
        Assert.True(rules.RequiresTwoHands);
        Assert.Equal(0, rules.InitiativeMod);
    }

    [Fact]
    public void Parse_TwoHandWeapons()
    {
        var rules = ParsedWeaponRules.Parse("+1 Attack, Parry (6+), Requires Two Hands");
        Assert.Equal(1, rules.ExtraAttacks);
        Assert.Equal(6, rules.Parry);
        Assert.True(rules.RequiresTwoHands);
    }

    [Fact]
    public void Parse_Polearm()
    {
        var rules = ParsedWeaponRules.Parse("+1 Initiative, Requires Two Hands");
        Assert.Equal(1, rules.InitiativeMod);
        Assert.True(rules.RequiresTwoHands);
        Assert.Null(rules.MightyBlow);
    }

    [Fact]
    public void Parse_HandWeapon_NoRules()
    {
        var rules = ParsedWeaponRules.Parse(null);
        Assert.Equal(0, rules.InitiativeMod);
        Assert.Equal(0, rules.ExtraAttacks);
        Assert.Null(rules.MightyBlow);
    }

    [Fact]
    public void Parse_Empty_NoRules()
    {
        var rules = ParsedWeaponRules.Parse("");
        Assert.Equal(0, rules.InitiativeMod);
        Assert.Null(rules.MightyBlow);
    }

    [Fact]
    public void Parse_SpearWithoutNotes_NoChargedBonus()
    {
        // Without notes, ChargedToFrontInitiativeBonus should be 0
        // and MB condition defaults to Always (notes needed to determine WhenBeingCharged)
        var rules = ParsedWeaponRules.Parse("+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*");
        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(0, rules.ChargedToFrontInitiativeBonus);
        Assert.Equal(WeaponCondition.Always, rules.MightyBlowCondition);
    }

    [Fact]
    public void Parse_LightLance_MBAndInitOnlyWhenCharging()
    {
        var rules = ParsedWeaponRules.Parse(
            "+1 Initiative*, Mighty Blow (1)*",
            "*Mounted model only. Only applies in turns that the model charges, and only against the unit they charged.");
        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.WhenCharging, rules.InitiativeModCondition);
        Assert.Equal(1, rules.MightyBlow);
        Assert.Equal(WeaponCondition.WhenCharging, rules.MightyBlowCondition);
    }

    [Fact]
    public void Parse_HeavyLance_MBAndInitOnlyWhenCharging()
    {
        var rules = ParsedWeaponRules.Parse(
            "+2 Initiative*, Mighty Blow (2)*",
            "*Mounted model only. Only applies in turns that the model charges, and only against the unit they charged.");
        Assert.Equal(2, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.WhenCharging, rules.InitiativeModCondition);
        Assert.Equal(2, rules.MightyBlow);
        Assert.Equal(WeaponCondition.WhenCharging, rules.MightyBlowCondition);
    }

    [Fact]
    public void Parse_Spear_MBExcludesLightTargets()
    {
        var rules = ParsedWeaponRules.Parse(
            "+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*",
            "Model on foot only. In turns in which the model is charged to its front, it receives an additional +1 to its Initiative.\n*Only applies in turns the model is charged to its front.\nMighty Blow does not apply towards Infantry, Large\nInfantry, Swarms and Shrines.");
        Assert.True(rules.MightyBlowExcludesLightTargets);
        Assert.Equal(WeaponCondition.WhenBeingCharged, rules.MightyBlowCondition);
    }
}
