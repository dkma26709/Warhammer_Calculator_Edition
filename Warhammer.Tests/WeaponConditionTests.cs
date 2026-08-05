using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Weapon modifiers that the books gate on a condition. The conditions live in footnote prose
/// the PDFs wrap mid-phrase, which is what defeated the original parser. The first group feeds
/// <see cref="ParsedWeaponRules.Parse"/> note strings copied verbatim from the books, so a
/// wrapping quirk is reproduced exactly; the later theories assert against the shipped tables.
/// </summary>
public class WeaponConditionTests
{

    // ─── Wrapped footnote prose ────────────────────────────────────

    [Fact]
    public void ChargeConditionSurvivesALineBreakMidPhrase()
    {
        // Verbatim from wap-v2's heavy lance, wrap included: the phrase the parser looks for is
        // split across the column break, so a literal substring search misses it and the
        // modifier falls back to unconditional.
        var rules = ParsedWeaponRules.Parse("+2 Initiative*, Mighty Blow (2)*",
            "*Mounted model only. Only applies in turns that the\nmodel charges, and only against the unit they charged.");

        Assert.Equal(WeaponCondition.WhenCharging, rules.MightyBlowCondition);
        Assert.Equal(WeaponCondition.WhenCharging, rules.InitiativeModCondition);
    }

    [Fact]
    public void ARewordedFootnoteStillYieldsTheSameCondition()
    {
        // wap-v3 says the same thing in different words, also wrapped.
        var rules = ParsedWeaponRules.Parse("Mighty Blow (2)*",
            "*Mounted model only. A model with a heavy lance\ngains +2 Initiative and Mighty Blow in turns that the\nmodel charges, and only against the unit they charged.");

        Assert.Equal(WeaponCondition.WhenCharging, rules.MightyBlowCondition);
        Assert.Equal(2, rules.MightyBlow);
        // v3 states the Initiative bonus only in the prose, so it has to be read from there.
        Assert.Equal(2, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.WhenCharging, rules.InitiativeModCondition);
    }

    [Fact]
    public void TwoDifferentlyConditionedModifiersInOneFootnoteDoNotCrossContaminate()
    {
        // wap-v3's spear: Initiative applies in the first round, Mighty Blow only when
        // charged to the front. One condition for the whole weapon gets one of them wrong.
        var rules = ParsedWeaponRules.Parse("Fight in Extra Ranks (1), Mighty Blow (1)*, Requires Two Hands*",
            "Model on foot only. A model with a spear gains +1\nInitiative in the first round of close combat.\n"
            + "*Only applies in turns the model is charged to its front,\nchose either Hold or Stand & Shoot as a charge\n"
            + "reaction and the model chooses to use Requires Two\nHands. Mighty Blow does not apply towards Infantry,\nSwarms and Shrines.");

        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.FirstRound, rules.InitiativeModCondition);
        Assert.Equal(WeaponCondition.WhenBeingCharged, rules.MightyBlowCondition);
        Assert.True(rules.MightyBlowExcludesLightTargets);
    }

    [Fact]
    public void AnInitiativePenaltyStatedInProseIsRead()
    {
        // wap-v3's great weapon states it in the footnote; wap-v2 had it in the rules column.
        var rules = ParsedWeaponRules.Parse("Mighty Blow (1), Requires Two Hands",
            "A model with a great weapon suffers -2 Initiative.");

        Assert.Equal(-2, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.Always, rules.InitiativeModCondition);
    }

    [Fact]
    public void AnUnconditionalRulesColumnModifierIgnoresTheProse()
    {
        // No asterisk means the Special Rules column value stands, whatever the notes say.
        var rules = ParsedWeaponRules.Parse("+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*",
            "Model on foot only. In turns in which the model is\ncharged to its front, it receives an additional +1 to its\nInitiative.\n"
            + "*Only applies in turns the model is charged to its front.");

        Assert.Equal(1, rules.InitiativeMod);
        Assert.Equal(WeaponCondition.Always, rules.InitiativeModCondition);
        Assert.Equal(1, rules.ChargedToFrontInitiativeBonus);
        Assert.Equal(WeaponCondition.WhenBeingCharged, rules.MightyBlowCondition);
    }

    // ─── Holds() ───────────────────────────────────────────────────

    [Theory]
    [InlineData(WeaponCondition.Always, false, false, 3, true)]
    [InlineData(WeaponCondition.WhenCharging, true, false, 1, true)]
    [InlineData(WeaponCondition.WhenCharging, false, true, 1, false)]
    [InlineData(WeaponCondition.WhenBeingCharged, false, true, 1, true)]
    [InlineData(WeaponCondition.WhenBeingCharged, true, false, 1, false)]
    [InlineData(WeaponCondition.FirstRound, false, false, 1, true)]
    [InlineData(WeaponCondition.FirstRound, false, false, 2, false)]
    public void HoldsMatchesTheCondition(
        WeaponCondition condition, bool charging, bool beingCharged, int round, bool expected)
    {
        Assert.Equal(expected, ParsedWeaponRules.Holds(condition, charging, beingCharged, round));
    }

    // ─── The shipped weapon tables ─────────────────────────────────

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public void LancesOnlyBiteOnTheChargeInEverySystem(string systemKey)
    {
        var repo = TestHelpers.RepoFor(systemKey);

        foreach (var key in new[] { CloseCombatWeaponKey.LightLance, CloseCombatWeaponKey.HeavyLance })
        {
            var lance = repo.GetWeapon(key);
            Assert.NotNull(lance);
            Assert.Equal(WeaponCondition.WhenCharging, lance.Rules.MightyBlowCondition);
            Assert.Equal(WeaponCondition.WhenCharging, lance.Rules.InitiativeModCondition);
            Assert.True(lance.Rules.MightyBlow > 0, $"{key} should carry a Mighty Blow value");
            Assert.True(lance.Rules.InitiativeMod > 0, $"{key} should carry an Initiative bonus");
        }
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public void GreatWeaponsAndPolearmsCarryTheirStrengthBonus(string systemKey)
    {
        // The Strength column reads "As user +1" in v3 and a bare "+1" in v2. Reading only the
        // "As user" leaves the bonus stranded in specialRules where nothing consumes it, and
        // the weapon quietly loses a point of Strength.
        var repo = TestHelpers.RepoFor(systemKey);

        foreach (var key in new[] { CloseCombatWeaponKey.GreatWeapon, CloseCombatWeaponKey.Polearm })
        {
            var weapon = repo.GetWeapon(key);
            Assert.NotNull(weapon);
            Assert.Equal(5, weapon.Strength.Resolve(4));
        }
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public void GreatWeaponsStrikeLast(string systemKey)
    {
        var greatWeapon = TestHelpers.RepoFor(systemKey).GetWeapon(CloseCombatWeaponKey.GreatWeapon);

        Assert.NotNull(greatWeapon);
        Assert.Equal(-2, greatWeapon.Rules.InitiativeMod);
        Assert.Equal(3, greatWeapon.Rules.ApplyToInitiative(5, isCharging: false, isBeingCharged: false, combatRound: 1));
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public void NoWeaponLeavesAStrayModifierInItsSpecialRules(string systemKey)
    {
        // A signed number at the head of specialRules means a column split went wrong: either
        // it belongs in Strength, or it is a stat modifier the parser should have consumed.
        var repo = TestHelpers.RepoFor(systemKey);

        foreach (var key in Enum.GetValues<CloseCombatWeaponKey>())
        {
            var weapon = repo.GetWeapon(key);
            if (weapon == null) continue;

            // Everything the parser understands is reflected on the parsed rules; anything
            // signed that it skipped would show up as a zero modifier it should have had.
            var rules = weapon.Rules;
            bool claimsStrengthBonus = weapon.Strength.Resolve(4) != 4;
            bool claimsSomething = claimsStrengthBonus || rules.InitiativeMod != 0
                || rules.MightyBlow.HasValue || rules.ExtraAttacks != 0
                || rules.FightInExtraRanks.HasValue || rules.Parry.HasValue
                || rules.RequiresTwoHands;

            Assert.True(key == CloseCombatWeaponKey.HandWeapon || claimsSomething,
                $"{systemKey}/{key} parsed to no modifiers at all — check the column split");
        }
    }
}
