using Warhammer.Models.Domain;

namespace Warhammer.Tests;

/// <summary>
/// Ward saves combine rather than one masking the other: they "may be combined with other Ward
/// saves as normal", but "no model may have a Ward save better than 4+ by combining multiple Ward
/// saves regardless of source" (2.32 p38; 3.11 p38 says the same of invulnerable saves, which is
/// what v3 calls them). The engine used to take the best single ward and never combine, so a
/// Tzeentch daemon with Daemonic's 5+ and its mark's 6+ saved on 5+ where the books give 4+.
/// </summary>
public class WardSaveCombiningTests
{
    private static int? Combine(params int[] wards) =>
        wards.Aggregate((ParsedSpecialRules?)null, (acc, w) =>
                (acc ?? new ParsedSpecialRules()).Merge(new ParsedSpecialRules { WardSave = w }))!
            .WardSave;

    [Fact]
    public void TwoSixesMakeAFive()
    {
        Assert.Equal(5, Combine(6, 6));
    }

    [Fact]
    public void TwoFivesAreCappedAtFour()
    {
        // 2 + 2 points would be a 3+; combining cannot beat 4+.
        Assert.Equal(4, Combine(5, 5));
    }

    /// <summary>The books' own worked example.</summary>
    [Fact]
    public void TwoSixesAndAFiveMakeAFour()
    {
        Assert.Equal(4, Combine(6, 6, 5));
    }

    [Fact]
    public void ManySixesStillCapAtFour()
    {
        Assert.Equal(4, Combine(6, 6, 6));
        Assert.Equal(4, Combine(6, 6, 6, 6, 6));
    }

    /// <summary>
    /// "Note that this does not stop single Ward saves from being used, such as a model having a
    /// listed 2+ or 3+ Ward save; the above limitation only applies to combining Ward saves."
    /// </summary>
    [Fact]
    public void ASingleWardKeepsItsListedValue()
    {
        Assert.Equal(2, Combine(2));
        Assert.Equal(3, Combine(3));
    }

    /// <summary>
    /// The player "may choose which Ward saves are used in any given situation", so a good listed
    /// ward is never made worse by acquiring a poor one.
    /// </summary>
    [Theory]
    [InlineData(2, 6, 2)]
    [InlineData(3, 6, 3)]
    [InlineData(2, 5, 2)]
    [InlineData(4, 6, 4)]
    public void AddingAPoorWardNeverWorsensAGoodOne(int listed, int extra, int expected)
    {
        Assert.Equal(expected, Combine(listed, extra));
        Assert.Equal(expected, Combine(extra, listed));
    }

    [Fact]
    public void OneSourceOnEitherSideIsKept()
    {
        Assert.Equal(6, new ParsedSpecialRules { WardSave = 6 }
            .Merge(new ParsedSpecialRules()).WardSave);
        Assert.Equal(6, new ParsedSpecialRules()
            .Merge(new ParsedSpecialRules { WardSave = 6 }).WardSave);
        Assert.Null(new ParsedSpecialRules().Merge(new ParsedSpecialRules()).WardSave);
    }

    /// <summary>
    /// A rule list carrying two ward sources combines them. Daemonic grants 5+ and the Mark of
    /// Tzeentch 6+, and Warriors of Chaos units routinely carry both.
    /// </summary>
    [Fact]
    public void TwoWardSourcesInOneRuleListCombine()
    {
        Assert.Equal(4, ParsedSpecialRules.Parse("Daemonic, Daemon of Tzeentch").WardSave);
        Assert.Equal(5, ParsedSpecialRules.Parse("Mark of Tzeentch, Blessing of the Lady").WardSave);
    }

    /// <summary>
    /// Regeneration is a ward by the house reading but deliberately does not combine — best-of,
    /// per feedback/warhammer. Pinned so the distinction stays a decision.
    /// </summary>
    [Fact]
    public void RegenerationStillTakesTheBestOfItsSources()
    {
        var merged = new ParsedSpecialRules { Regeneration = 5 }
            .Merge(new ParsedSpecialRules { Regeneration = 6 });

        Assert.Equal(5, merged.Regeneration);
    }

    [Fact]
    public void ParryAndDodgeStillTakeTheBestOfTheirSources()
    {
        var merged = new ParsedSpecialRules { Parry = 6, Dodge = 5 }
            .Merge(new ParsedSpecialRules { Parry = 5, Dodge = 6 });

        Assert.Equal(5, merged.Parry);
        Assert.Equal(5, merged.Dodge);
    }
}
