using System.Text.RegularExpressions;
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

    /// <summary>The marker leads as often as it trails, and led on a rule two units still have.</summary>
    [Fact]
    public void ALeadingAsteriskDoesNotDropTheRule()
    {
        var rules = ParsedSpecialRules.Parse("*Random Attacks (D6+2)");

        Assert.Equal("D6+2", rules.RandomAttacksRoll);
        Assert.Equal(["Random Attacks (D6+2)"], rules.AllRuleNames);
    }

    [Fact]
    public async Task ScylaAnfingrimmKeepsHisRandomAttacks()
    {
        foreach (var system in new[] { GameSystems.WapV2, GameSystems.WapV3 })
        {
            var scyla = (await TestHelpers.RepoFor(system).GetUnitsAsync(FactionKey.WarriorsOfChaos))
                .First(u => u.Name == "Scyla Anfingrimm");

            Assert.Equal("D6+2", scyla.BaseModel.SpecialRules.RandomAttacksRoll);
        }
    }

    // ─── A bulleted rule definition, not a rule list ───────────────

    /// <summary>
    /// Where a rule belongs to one unit alone the books write it out in full instead of naming it
    /// in the comma-separated list, and the extractor keeps the whole paragraph. Comma-splitting
    /// it served the entire sentence as one badge plus a tail of prose fragments; only the name in
    /// front of the colon is the rule.
    /// </summary>
    [Fact]
    public void ABulletedDefinitionIsReducedToItsName()
    {
        var rules = ParsedSpecialRules.Parse(
            "• Militia: Models with this special rule can use the Support Fire and Support Charge "
            + "actions from the State Troops special rule.");

        Assert.Equal(["Militia"], rules.AllRuleNames);
    }

    [Fact]
    public async Task FreeCompanyMilitiaShowOneBadgeReadingMilitia()
    {
        foreach (var system in new[] { GameSystems.WapV2, GameSystems.WapV3 })
        {
            var militia = (await TestHelpers.RepoFor(system).GetUnitsAsync(FactionKey.TheEmpire))
                .First(u => u.Name == "Free Company Militia");

            Assert.Equal(["Militia"], militia.BaseModel.SpecialRules.AllRuleNames);
        }
    }

    /// <summary>A named rule still parses when the books define it rather than list it.</summary>
    [Fact]
    public void ABulletedDefinitionStillReachesItsArm()
    {
        Assert.Equal(5, ParsedSpecialRules
            .Parse("• Daemonic: A Verminlord has the Magical Attacks and Unbreakable special rules.")
            .WardSave);
    }

    /// <summary>A list can carry both forms — names first, then the definitions.</summary>
    [Fact]
    public void ADefinitionAfterAListKeepsBoth()
    {
        var rules = ParsedSpecialRules.Parse(
            "Ethereal, Terror, Undead • If I Cannot Rule, None Shall Rule!: Kurdoss Valentian or "
            + "any other Army General cannot use their Inspiring Presence within 12\" of him.");

        Assert.Equal(
            ["Ethereal", "Terror", "Undead", "If I Cannot Rule, None Shall Rule!"],
            rules.AllRuleNames);
        Assert.True(rules.Unstable);    // from Undead, which the prose used to swallow
    }

    /// <summary>A bullet that defines nothing — an options line — contributes no badge.</summary>
    [Fact]
    public void ABulletWithoutANameIsNotABadge()
    {
        Assert.Equal(["Stubborn"], ParsedSpecialRules
            .Parse("Stubborn • May take a shield for +2 points per model")
            .AllRuleNames);
    }

    // ─── The next page's banner ran onto the last rule ─────────────

    /// <summary>
    /// The extractor appends the following page's heading to the final rule. The books never set a
    /// rule name in capitals, so a trailing all-caps run is always the banner — and left on, it
    /// took the rule with it.
    /// </summary>
    [Theory]
    [InlineData("Death Shriek, Ethereal, Fly (6), Terror, Undead CHARACTERS")]
    [InlineData("Hatred, Mark of Khorne, Unbreakable NURGLE SPECIAL CHARACTERS")]
    [InlineData("Chaos Armour, Mark of Tzeentch, Unbreakable MARKED SPECIAL CHARACTERS (LORDS)")]
    public void ATrailingPageBannerDoesNotDropTheRule(string text)
    {
        var rules = ParsedSpecialRules.Parse(text);

        Assert.DoesNotContain(rules.AllRuleNames, n => n.Contains("CHARACTERS"));
        Assert.True(rules.Unstable || rules.Unbreakable);
    }

    /// <summary>A rule name that merely ends in a capitalised word is not a banner.</summary>
    [Fact]
    public void ARuleNameEndingInACapitalisedWordSurvives()
    {
        Assert.Equal(["Hatred (Chaos, Undead, Wizards)", "Waaagh!", "Fly (9)"],
            ParsedSpecialRules.Parse("Hatred (Chaos, Undead, Wizards), Waaagh!, Fly (9)").AllRuleNames);
    }

    [Fact]
    public async Task TheV3TombBansheeIsStillUndead()
    {
        var banshee = (await TestHelpers.RepoFor(GameSystems.WapV3).GetUnitsAsync(FactionKey.VampireCounts))
            .First(u => u.Name == "Tomb Banshee");

        Assert.True(banshee.BaseModel.SpecialRules.Unstable);
        Assert.True(banshee.BaseModel.SpecialRules.Fear);
    }

    [Fact]
    public async Task TheV3ScylaAnfingrimmIsStillUnbreakable()
    {
        var scyla = (await TestHelpers.RepoFor(GameSystems.WapV3).GetUnitsAsync(FactionKey.WarriorsOfChaos))
            .First(u => u.Name == "Scyla Anfingrimm");

        Assert.True(scyla.BaseModel.SpecialRules.Unbreakable);
    }

    // ─── A cross-reference is not part of the name ─────────────────

    /// <summary>"(see Cairn Wraiths)" points at where the book writes the rule out. It is not a
    /// value, not a scope, and not part of what the unit's badge should read.</summary>
    [Fact]
    public void ACrossReferenceIsNotShownInTheRuleName()
    {
        Assert.Equal(["Chill Grasp"],
            ParsedSpecialRules.Parse("Chill Grasp (see Cairn Wraiths)").AllRuleNames);
        Assert.Equal(["Battle Prayers of Sigmar"],
            ParsedSpecialRules.Parse("Battle Prayers of Sigmar (see Warrior Priest)").AllRuleNames);
    }

    /// <summary>A scope alongside the cross-reference has to survive it — it picks the model.</summary>
    [Fact]
    public void ACrossReferenceDoesNotTakeTheScopeWithIt()
    {
        var sets = ParsedSpecialRules.ParseScoped(
            "Chill Grasp (Kurdoss Valentian only, see Cairn Wraiths)");

        Assert.Equal(["Chill Grasp (Kurdoss Valentian only)"], sets.UnitWide.AllRuleNames);
        Assert.Equal("Kurdoss Valentian", Assert.Single(sets.Scoped).Scope);
    }

    // ─── PDF line-break and transposition artefacts ────────────────

    /// <summary>
    /// A hyphenated word broken across lines kept the gap, so one rule reached the badges under
    /// two spellings — "Wall- crawler" beside "Wall-crawler".
    /// </summary>
    [Fact]
    public void AHyphenBrokenAcrossLinesIsRejoined()
    {
        Assert.Equal(["Wall-crawler"], ParsedSpecialRules.Parse("Wall- crawler").AllRuleNames);

        // Both spellings of the same Empire rule, which the badges used to show side by side.
        Assert.Equal(
            ParsedSpecialRules.Parse("Righteous Fury (see Arch-Lector)").AllRuleNames,
            ParsedSpecialRules.Parse("Righteous Fury (see Arch- Lector)").AllRuleNames);
    }

    /// <summary>wap-v2's Chaos Trolls have "Natural Armour (+6)" — the save with its sign moved.</summary>
    [Fact]
    public void ATransposedSaveStillParses()
    {
        Assert.Equal(6, ParsedSpecialRules.Parse("Natural Armour (+6)").NaturalArmour);
        Assert.Equal(6, ParsedSpecialRules.Parse("Natural Armour (6+)").NaturalArmour);
    }

    [Fact]
    public async Task ChaosTrollsHaveTheirNaturalArmour()
    {
        var trolls = (await TestHelpers.RepoFor(GameSystems.WapV2).GetUnitsAsync(FactionKey.WarriorsOfChaos))
            .First(u => u.Name == "Chaos Trolls");

        Assert.Equal(6, trolls.BaseModel.SpecialRules.NaturalArmour);
    }

    /// <summary>
    /// The books capitalise every rule name, so a lowercase word after a comma continues the name
    /// it follows. Lokhir Fellheart's "At them, you Curs!" was served as two badges.
    /// </summary>
    [Fact]
    public void ALowercaseFragmentContinuesTheNameBeforeIt()
    {
        Assert.Equal(["Ambushers", "At them, you Curs!", "Elven Grace"],
            ParsedSpecialRules
                .Parse("Ambushers, At them, you Curs! (see Black Ark Fleetmaster), Elven Grace")
                .AllRuleNames);
    }

    // ─── Corpus-wide guard ─────────────────────────────────────────

    /// <summary>
    /// Every rule name every shipped unit displays, held to the shape of a name. This is the guard
    /// that caught the cases above: each was a badge holding a sentence, a page banner or a prose
    /// fragment, and none was visible from a unit test written against a hand-typed string.
    /// </summary>
    [Fact]
    public async Task EveryShippedRuleNameLooksLikeARuleName()
    {
        // wap-v3's Skink Cohort reads "Poisoned Attacks (" — the qualifier is missing from the
        // extracted data, so the rule cannot be resolved without the army book. Listed here so the
        // guard stays green while the gap stays visible.
        string[] knownTruncations = ["Poisoned Attacks ("];

        var complaints = new List<string>();

        foreach (var system in new[] { GameSystems.WapV2, GameSystems.WapV3, GameSystems.War })
        {
            var repo = TestHelpers.RepoFor(system);
            foreach (var faction in repo.AllFactions)
            {
                foreach (var unit in await repo.GetUnitsAsync(faction))
                {
                    foreach (var name in unit.BaseModel.SpecialRules.AllRuleNames)
                    {
                        if (knownTruncations.Contains(name)) continue;

                        var fault = NameFault(name);
                        if (fault != null)
                            complaints.Add($"{system}/{faction}/{unit.Name}: {fault} — \"{name}\"");
                    }
                }
            }
        }

        Assert.True(complaints.Count == 0,
            $"{complaints.Count} rule name(s) are not names:\n" + string.Join("\n", complaints));
    }

    private static string? NameFault(string name) => name switch
    {
        _ when name.Length == 0 => "empty",
        _ when name.StartsWith('•') => "kept its bullet",
        _ when name.Contains(':') => "carries a definition",
        _ when char.IsLower(name[0]) => "is a prose fragment",
        _ when name.Contains("(see ", StringComparison.OrdinalIgnoreCase) => "kept a cross-reference",
        _ when name.StartsWith('*') => "kept a footnote marker",
        _ when CountsUnbalancedParens(name) => "has an unclosed parenthesis",
        _ when Regex.IsMatch(name, @"\b[A-Z]{4,}\b") => "ran into a page banner",
        _ when Regex.IsMatch(name, @"\p{L}-\s") => "kept a broken hyphen",
        _ => null,
    };

    private static bool CountsUnbalancedParens(string name) =>
        name.Count(c => c == '(') != name.Count(c => c == ')');
}
