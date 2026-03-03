using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class SpecialRulesParsingTests
{
    [Fact]
    public void Parse_KillingBlow()
    {
        var rules = ParsedSpecialRules.Parse("Killing Blow");
        Assert.True(rules.KillingBlow);
    }

    [Fact]
    public void Parse_WardSave()
    {
        var rules = ParsedSpecialRules.Parse("Ward save (5+)");
        Assert.Equal(5, rules.WardSave);
    }

    [Fact]
    public void Parse_NaturalArmour()
    {
        var rules = ParsedSpecialRules.Parse("Natural Armour (6+)");
        Assert.Equal(6, rules.NaturalArmour);
    }

    [Fact]
    public void Parse_Frenzy()
    {
        var rules = ParsedSpecialRules.Parse("Frenzy");
        Assert.True(rules.Frenzy);
    }

    [Fact]
    public void Parse_Hatred()
    {
        var rules = ParsedSpecialRules.Parse("Hatred");
        Assert.True(rules.Hatred);
    }

    [Fact]
    public void Parse_AlwaysStrikesFirst()
    {
        var rules = ParsedSpecialRules.Parse("Always Strikes First");
        Assert.True(rules.AlwaysStrikesFirst);
    }

    [Fact]
    public void Parse_AlwaysStrikesLast()
    {
        var rules = ParsedSpecialRules.Parse("Always Strikes Last");
        Assert.True(rules.AlwaysStrikesLast);
    }

    [Fact]
    public void Parse_ImpactHits_D3()
    {
        var rules = ParsedSpecialRules.Parse("Impact Hits (D3)");
        Assert.Null(rules.ImpactHits);
        Assert.Equal("D3", rules.ImpactHitsRoll);
    }

    [Fact]
    public void Parse_ImpactHits_Fixed()
    {
        var rules = ParsedSpecialRules.Parse("Impact Hits (2)");
        Assert.Equal(2, rules.ImpactHits);
    }

    [Fact]
    public void Parse_FightInExtraRanks()
    {
        var rules = ParsedSpecialRules.Parse("Fight in Extra Ranks (1)");
        Assert.Equal(1, rules.FightInExtraRanks);
    }

    [Fact]
    public void Parse_MightyBlow()
    {
        var rules = ParsedSpecialRules.Parse("Mighty Blow (2)");
        Assert.Equal(2, rules.MightyBlow);
    }

    [Fact]
    public void Parse_PoisonedAttacks()
    {
        var rules = ParsedSpecialRules.Parse("Poisoned Attacks");
        Assert.True(rules.PoisonedAttacks);
    }

    [Fact]
    public void Parse_FlamingAttacks()
    {
        var rules = ParsedSpecialRules.Parse("Flaming Attacks");
        Assert.True(rules.FlamingAttacks);
    }

    [Fact]
    public void Parse_MultipleWounds_D3()
    {
        var rules = ParsedSpecialRules.Parse("Multiple Wounds (D3)");
        Assert.Null(rules.MultipleWounds);
        Assert.Equal("D3", rules.MultipleWoundsRoll);
    }

    [Fact]
    public void Parse_MultipleWounds_Fixed()
    {
        var rules = ParsedSpecialRules.Parse("Multiple Wounds (2)");
        Assert.Equal(2, rules.MultipleWounds);
    }

    [Fact]
    public void Parse_CommaSeparated_MultipleRules()
    {
        var rules = ParsedSpecialRules.Parse("Killing Blow, Frenzy, Ward save (4+), Hatred");
        Assert.True(rules.KillingBlow);
        Assert.True(rules.Frenzy);
        Assert.Equal(4, rules.WardSave);
        Assert.True(rules.Hatred);
    }

    [Fact]
    public void Parse_NewlinePollution_TruncatesAtUppercase()
    {
        var rules = ParsedSpecialRules.Parse("Killing Blow, Frenzy\nSOME DESCRIPTION that should be ignored");
        Assert.True(rules.KillingBlow);
        Assert.True(rules.Frenzy);
    }

    [Fact]
    public void Parse_Ethereal()
    {
        var rules = ParsedSpecialRules.Parse("Ethereal");
        Assert.True(rules.Ethereal);
    }

    [Fact]
    public void Parse_Fear()
    {
        var rules = ParsedSpecialRules.Parse("Fear");
        Assert.True(rules.Fear);
    }

    [Fact]
    public void Parse_Terror()
    {
        var rules = ParsedSpecialRules.Parse("Terror");
        Assert.True(rules.Terror);
    }

    [Fact]
    public void Parse_Regeneration_Default()
    {
        var rules = ParsedSpecialRules.Parse("Regeneration");
        Assert.Equal(4, rules.Regeneration);
    }

    [Fact]
    public void Parse_Regeneration_WithValue()
    {
        var rules = ParsedSpecialRules.Parse("Regeneration (5+)");
        Assert.Equal(5, rules.Regeneration);
    }

    [Fact]
    public void Parse_ArmourPiercing()
    {
        var rules = ParsedSpecialRules.Parse("Armour Piercing (2)");
        Assert.Equal(2, rules.ArmourPiercing);
    }

    [Fact]
    public void Parse_DevastatingCharge()
    {
        var rules = ParsedSpecialRules.Parse("Devastating Charge");
        Assert.True(rules.DevastatingCharge);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsDefaults()
    {
        var rules1 = ParsedSpecialRules.Parse(null);
        Assert.False(rules1.KillingBlow);
        Assert.Null(rules1.WardSave);

        var rules2 = ParsedSpecialRules.Parse("");
        Assert.False(rules2.KillingBlow);
    }

    [Fact]
    public void Parse_Stubborn()
    {
        var rules = ParsedSpecialRules.Parse("Stubborn");
        Assert.True(rules.Stubborn);
    }

    [Fact]
    public void Parse_Unbreakable()
    {
        var rules = ParsedSpecialRules.Parse("Unbreakable");
        Assert.True(rules.Unbreakable);
    }

    [Fact]
    public void Parse_MagicalAttacks()
    {
        var rules = ParsedSpecialRules.Parse("Magical Attacks");
        Assert.True(rules.MagicalAttacks);
    }

    [Fact]
    public void Parse_AllRuleNames_Populated()
    {
        var rules = ParsedSpecialRules.Parse("Killing Blow, Ward save (5+)");
        Assert.Equal(2, rules.AllRuleNames.Count);
        Assert.Contains("Killing Blow", rules.AllRuleNames);
        Assert.Contains("Ward save (5+)", rules.AllRuleNames);
    }
}
