using Warhammer.Models.Domain;

namespace Warhammer.Tests;

/// <summary>
/// Immunity (Psychology) / (Fear) cancels a Fear-causing enemy's Combat Resolution bonus.
/// It parsed to nothing before, so every unit carrying it conceded the bonus anyway.
/// </summary>
public class FearImmunityTests
{
    [Theory]
    [InlineData("Immunity (Psychology)")]
    [InlineData("Immunity (Fear)")]
    [InlineData("Immunity (Terror)")]
    [InlineData("Immunity (Fear/Terror)")]
    [InlineData("Hatred, Immunity (Psychology), Stubborn")]
    [InlineData("Immunity (Lightning Attacks, Psychology)")]
    public void PsychologyImmunitiesParseToFearImmunity(string specialRules)
    {
        Assert.True(ParsedSpecialRules.Parse(specialRules).ImmunityFear);
    }

    [Theory]
    [InlineData("Immunity (Killing Blow)")]
    [InlineData("Immunity (Poisoned Attacks)")]
    [InlineData("Immunity (Flaming Attacks)")]
    [InlineData("Stubborn, Hatred")]
    public void OtherImmunitiesDoNotGrantIt(string specialRules)
    {
        Assert.False(ParsedSpecialRules.Parse(specialRules).ImmunityFear);
    }

    [Fact]
    public void ImmunityToOneThingDoesNotSwallowTheOthers()
    {
        var rules = ParsedSpecialRules.Parse("Immunity (Killing Blow, Psychology)");

        Assert.True(rules.ImmunityKillingBlow);
        Assert.True(rules.ImmunityFear);
        Assert.False(rules.ImmunityPoisonedAttacks);
    }

    [Fact]
    public void MergePropagatesIt()
    {
        var plain = ParsedSpecialRules.Parse("Stubborn");
        var immune = ParsedSpecialRules.Parse("Immunity (Psychology)");

        Assert.True(plain.Merge(immune).ImmunityFear);
        Assert.True(immune.Merge(plain).ImmunityFear);
    }
}
