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
        Assert.False(rules.MightyBlowOnlyWhenCharged);
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
        Assert.True(rules.MightyBlowOnlyWhenCharged);
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
        var rules = ParsedWeaponRules.Parse("+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*");
        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(0, rules.ChargedToFrontInitiativeBonus);
        Assert.True(rules.MightyBlowOnlyWhenCharged);
    }
}
