using Warhammer.Calculator;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Rules that reached no arm in <see cref="ParsedSpecialRules.Parse"/> and were dropped in
/// silence. Each case below was found in shipped faction data, so every assertion doubles as a
/// regression guard on a real unit rather than only on a hand-written string.
/// </summary>
public class RuleNameParsingTests
{
    // ─── wap-v3 renamed Ward Save to Magical Ward ──────────────────

    /// <summary>
    /// "Models with this rule gain an invulnerable save as indicated in the brackets" (3.11 p63),
    /// and an invulnerable save is v2's ward save — after armour, never modified by the attack's
    /// Strength (3.11 p38). Without this name every v3 ward save silently vanished.
    /// </summary>
    [Fact]
    public void MagicalWardIsAWardSave()
    {
        Assert.Equal(6, ParsedSpecialRules.Parse("Magical Ward (6+)").WardSave);
        Assert.Equal(4, ParsedSpecialRules.Parse("Magical Ward (4+)").WardSave);
    }

    /// <summary>The v2 spelling has to keep working — v2 army books never say Magical Ward.</summary>
    [Fact]
    public void WardSaveStillParsesUnderTheV2Spelling()
    {
        Assert.Equal(5, ParsedSpecialRules.Parse("Ward Save (5+)").WardSave);
        Assert.Equal(6, ParsedSpecialRules.Parse("Ward save (6+)").WardSave);
    }

    [Fact]
    public async Task V3WardancersHaveTheirWardSave()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var wardancers = (await repo.GetUnitsAsync(FactionKey.WoodElves))
            .First(u => u.Name == "Wardancers");

        Assert.Equal(6, wardancers.BaseModel.SpecialRules.WardSave);
    }

    // ─── Tomb Kings' Indomitable (wap-v3) ──────────────────────────

    [Fact]
    public void IndomitableParses()
    {
        Assert.True(ParsedSpecialRules.Parse("Curse of the Necropolis, Flammable, Indomitable, Undead")
            .Indomitable);
    }

    /// <summary>
    /// "suffer 1 Wound less than they normally would due to the Unstable special rule"
    /// (Tomb Kings 3.1 p4), on top of the Unstable rule's own steadfast reduction.
    /// </summary>
    [Theory]
    [InlineData(false, false, 4)]   // plain Unstable: wounds equal the CR deficit
    [InlineData(true, false, 3)]    // steadfast: one less
    [InlineData(false, true, 3)]    // Indomitable: one less
    [InlineData(true, true, 2)]     // both reductions apply
    public void IndomitableReducesUnstableWounds(bool steadfast, bool indomitable, int expected)
    {
        var rules = new ParsedSpecialRules { Unstable = true, Indomitable = indomitable };
        var result = BreakTestResolver.Resolve(
            crDifference: 4,
            leadership: 8,
            rules,
            unitRanks: steadfast ? 3 : 1,
            opponentRanks: 2,
            new Random(1));

        Assert.Equal(BreakTestOutcome.UnstableWounds, result.Outcome);
        Assert.Equal(expected, result.UnstableWoundsDealt);
    }

    [Fact]
    public void UnstableWoundsNeverGoNegative()
    {
        var rules = new ParsedSpecialRules { Unstable = true, Indomitable = true, Stubborn = true };
        var result = BreakTestResolver.Resolve(1, 8, rules, 1, 5, new Random(1));

        Assert.Equal(0, result.UnstableWoundsDealt);
    }

    // ─── A trailing full stop dropped the rule ─────────────────────

    /// <summary>
    /// The books end the last rule of a list with a full stop. Left on, it either rode along in
    /// the name or defeated the parenthetical match, and the rule reached no arm either way.
    /// </summary>
    [Fact]
    public void ATrailingFullStopDoesNotDropABareRule()
    {
        var rules = ParsedSpecialRules.Parse("Immunity (Psychology), Stubborn.");

        Assert.True(rules.Stubborn);
        Assert.True(rules.ImmunityFear);
    }

    [Fact]
    public void ATrailingFullStopDoesNotDropAParameterisedRule()
    {
        Assert.Equal(3, ParsedSpecialRules.Parse("Fly (7), Natural Armour (3+).").NaturalArmour);
        Assert.Equal(2, ParsedSpecialRules.Parse("Natural Armour (2+).").NaturalArmour);
        Assert.Equal(4, ParsedSpecialRules.Parse("Regeneration (4+).").Regeneration);
    }

    /// <summary>
    /// "Undead." carries both Fear and Unstable, so the full stop cost three Tomb Kings units
    /// two rules apiece.
    /// </summary>
    [Fact]
    public void ATrailingFullStopDoesNotDropACompoundRule()
    {
        var rules = ParsedSpecialRules.Parse("Fear, Flammable, Undead.");

        Assert.True(rules.Unstable);
        Assert.True(rules.Fear);
    }

    /// <summary>The full stop should not survive into the displayed rule name either.</summary>
    [Fact]
    public void ATrailingFullStopIsNotKeptInTheRuleName()
    {
        Assert.Contains("Stubborn", ParsedSpecialRules.Parse("Stubborn.").AllRuleNames);
    }

    [Fact]
    public async Task TheStarDragonKeepsItsNaturalArmour()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV2);
        var dragon = (await repo.GetUnitsAsync(FactionKey.HighElves))
            .First(u => u.Name == "Star Dragon");

        Assert.Equal(2, dragon.BaseModel.SpecialRules.NaturalArmour);
    }

    [Fact]
    public async Task GiantsAreStubbornInBothSystems()
    {
        foreach (var system in new[] { GameSystems.WapV2, GameSystems.WapV3 })
        {
            var repo = TestHelpers.RepoFor(system);
            var giant = (await repo.GetUnitsAsync(FactionKey.OrcsAndGoblins))
                .FirstOrDefault(u => u.Name == "Giant");
            if (giant == null)
                continue;

            Assert.True(giant.BaseModel.SpecialRules.Stubborn, $"Giant not Stubborn in {system}");
        }
    }

    // ─── An asterisk footnote marker must still be tolerated ───────

    [Fact]
    public void AnAsteriskStillParsesAlongsideTheFullStopFix()
    {
        Assert.Equal(1, ParsedSpecialRules.Parse("Mighty Blow (1)*").MightyBlow);
        Assert.True(ParsedSpecialRules.Parse("Stubborn*").Stubborn);
        Assert.True(ParsedSpecialRules.Parse("Stubborn.*").Stubborn);
    }
}
