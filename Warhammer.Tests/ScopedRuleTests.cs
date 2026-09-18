using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Rules the army books scope to one model in a unit — "Poisoned Attacks (Steed of Slaanesh
/// only)". They used to be mishandled in both directions: the two-parenthetical form reached no
/// arm at all and was dropped, and the one-parenthetical form parsed cleanly and was applied to
/// the whole unit, arming every Daemonette with its Steed of Slaanesh's poison.
/// </summary>
public class ScopedRuleTests
{
    // ─── Matching the book's name to a profile ─────────────────────

    [Theory]
    [InlineData("War Boars only", "War Boar")]              // the book pluralises
    [InlineData("Zlaaq only", "Zlaaq (Stegadon)")]           // the profile carries an aside
    [InlineData("Orc brute only", "Brute")]                  // the book qualifies
    [InlineData("Salamander only", "Ancient Salamander")]    // the profile qualifies
    [InlineData("Grymloq only", "Grymloq (Carnosaur)")]
    [InlineData("Slave Master only", "Slave Master")]
    public void AScopeNamesItsProfile(string scope, string profile)
    {
        Assert.True(ModelScope.MatchStrength(scope, profile) > 0);
    }

    [Theory]
    [InlineData("War Boars only", "Orc")]
    [InlineData("Ripperdactyl only", "Skink Brave")]
    [InlineData("Herder only", "Cave Squig")]
    public void AScopeDoesNotNameAnUnrelatedProfile(string scope, string profile)
    {
        Assert.Equal(0, ModelScope.MatchStrength(scope, profile));
    }

    /// <summary>
    /// "Slave Master only" contains "Slave", so first-match wins would pick the wrong profile.
    /// An exact match has to beat a containment one.
    /// </summary>
    [Fact]
    public void AnExactMatchBeatsAContainmentMatch()
    {
        Assert.Equal(1, ModelScope.BestMatch("Slave Master only", ["Slave", "Slave Master"]));
        Assert.Equal(0, ModelScope.BestMatch("Slave only", ["Slave", "Slave Master"]));
    }

    [Fact]
    public void AScopeNamingNothingMatchesNothing()
    {
        Assert.Null(ModelScope.BestMatch("Pox Maggoth only", ["Grumlok", "Gazbag"]));
        Assert.Null(ModelScope.BestMatch("War Boars only", [null]));
    }

    // ─── The parser separates scoped from unit-wide ────────────────

    [Fact]
    public void ScopedRulesAreKeptApartFromUnitWideOnes()
    {
        var parsed = ParsedSpecialRules.ParseScoped(
            "Animosity, Mighty Blow (1), Mighty Blow (2) (War Boars only), Natural Armour (6+)");

        Assert.Equal(1, parsed.UnitWide.MightyBlow);
        Assert.Equal(6, parsed.UnitWide.NaturalArmour);

        var scoped = Assert.Single(parsed.Scoped);
        Assert.Equal("War Boars", scoped.Scope);
        Assert.Equal(2, scoped.Rules.MightyBlow);
    }

    /// <summary>
    /// The single-parenthetical form is the one that used to be applied unit-wide. It has to leave
    /// the unit-wide set empty and carry its value into the scoped set instead.
    /// </summary>
    [Fact]
    public void ASingleParentheticalScopeIsNotAppliedUnitWide()
    {
        var parsed = ParsedSpecialRules.ParseScoped(
            "Daemon of Slaanesh, Fast Cavalry, Poisoned Attacks (Steed of Slaanesh only)");

        Assert.False(parsed.UnitWide.PoisonedAttacks);

        var scoped = Assert.Single(parsed.Scoped);
        Assert.Equal("Steed of Slaanesh", scoped.Scope);
        Assert.True(scoped.Rules.PoisonedAttacks);
    }

    /// <summary>A parameter that carries the rule's value must not be read as a scope.</summary>
    [Fact]
    public void AValueParameterIsNotAScope()
    {
        var parsed = ParsedSpecialRules.ParseScoped(
            "Natural Armour (6+), Regeneration (4+), Multiple Wounds (D3), Immunity (Psychology)");

        Assert.Empty(parsed.Scoped);
        Assert.Equal(6, parsed.UnitWide.NaturalArmour);
        Assert.Equal(4, parsed.UnitWide.Regeneration);
        Assert.True(parsed.UnitWide.ImmunityFear);
    }

    /// <summary>A cross-reference inside the scope is not part of the model's name.</summary>
    [Fact]
    public void ACrossReferenceIsStrippedFromTheScope()
    {
        var parsed = ParsedSpecialRules.ParseScoped(
            "Hatred (Chaos) (Johann van Hal only, see Witch Hunter Captain)");

        Assert.Equal("Johann van Hal", Assert.Single(parsed.Scoped).Scope);
    }

    /// <summary>A scoped rule still belongs in a rules display, wherever it ends up applying.</summary>
    [Fact]
    public void ScopedRulesStillAppearInTheRuleNameList()
    {
        var parsed = ParsedSpecialRules.ParseScoped("Mighty Blow (2) (War Boars only), Animosity");

        Assert.Contains("Mighty Blow (2) (War Boars only)", parsed.UnitWide.AllRuleNames);
        Assert.Contains("Animosity", parsed.UnitWide.AllRuleNames);
    }

    /// <summary>
    /// It appears once, under its full name. Merge concatenates AllRuleNames, so carrying the
    /// inner parse's copy across would list the rule a second time, stripped of its scope, on
    /// whichever model it landed on.
    /// </summary>
    [Fact]
    public async Task AScopedRuleIsListedOnceAndKeepsItsScope()
    {
        var boarBoyz = await TestHelpers.FindUnitAsync("orcs-goblins", "BOAR BOYZ");
        var names = boarBoyz.BaseModel.SpecialRules.AllRuleNames;

        Assert.Equal(1, names.Count(n => n == "Tusker Charge (War Boar only)"));
        Assert.DoesNotContain("Tusker Charge", names);
    }

    // ─── Routing onto the right model ──────────────────────────────

    /// <summary>
    /// Ripperdactyl Riders were the clearest over-application: the Skink Brave was getting the
    /// beast's Armour Piercing.
    /// </summary>
    [Fact]
    public async Task RipperdactylRulesBelongToTheRipperdactyl()
    {
        var riders = await TestHelpers.FindUnitAsync("lizardmen", "RIPPERDACTYL RIDERS");

        Assert.Null(riders.BaseModel.SpecialRules.ArmourPiercing);
        Assert.Equal(1, riders.Mount!.SpecialRules.ArmourPiercing);
    }

    [Fact]
    public async Task SeekersGiveTheirPoisonToTheSteed()
    {
        var seekers = await TestHelpers.FindUnitAsync("daemons-of-chaos", "SEEKERS");

        Assert.False(seekers.BaseModel.SpecialRules.PoisonedAttacks);
        Assert.True(seekers.Mount!.SpecialRules.PoisonedAttacks);
    }

    /// <summary>
    /// A scope naming the unit's own fighting profile belongs to the unit — this is the direction
    /// that was silently dropped rather than over-applied.
    /// </summary>
    [Fact]
    public async Task ASalamandersOwnNaturalArmourApplies()
    {
        var salamander = await TestHelpers.FindUnitAsync("lizardmen", "SALAMANDER");

        // "Natural Armour (5+) (Salamander only)" is the Salamander's; the Skink Handler's is
        // not, and the handler is not fielded as a fighting model.
        Assert.Equal(5, salamander.BaseModel.SpecialRules.NaturalArmour);
    }

    /// <summary>
    /// A champion-only rule goes nowhere. The engine fields one profile for the rank and file, so
    /// there is no model to put a Slavemaster's Murderous Prowess on — and giving it to all
    /// twenty slaves, which is what happened before, is worse than leaving it out.
    /// </summary>
    [Fact]
    public async Task AChampionOnlyRuleIsNotGivenToTheWholeUnit()
    {
        var slaves = await TestHelpers.FindUnitAsync("dark-elves", "SLAVES");

        Assert.False(slaves.BaseModel.SpecialRules.MurderousProwess);
        Assert.False(slaves.BaseModel.SpecialRules.Hatred);
        Assert.False(slaves.BaseModel.SpecialRules.ElvenGrace);
    }

    // ─── It reaches the combat maths ───────────────────────────────

    /// <summary>
    /// "Tusker Charge (War Boar only)" — "Models with this special rule have the Mighty Blow (1)
    /// special rule in any turn that they charge" — is scoped to the boar. With no arm for it,
    /// Boar Boyz had no boar bonus at all.
    /// </summary>
    [Fact]
    public void TuskerChargeIsMightyBlowOnTheCharge()
    {
        var parsed = ParsedSpecialRules.ParseScoped("Animosity, Tusker Charge (War Boar only)");
        var scoped = Assert.Single(parsed.Scoped);

        Assert.Equal("War Boar", scoped.Scope);
        Assert.True(scoped.Rules.TuskerCharge);
        Assert.False(parsed.UnitWide.TuskerCharge);
    }

    /// <summary>
    /// Unlike a plain Mighty Blow it is not confined to the first round — "in any turn that they
    /// charge" — and it adds to whatever the unit already had.
    /// </summary>
    [Theory]
    [InlineData(true, 1, 3, 5)]     // charging, round 1: unit's 1 plus the charge's 1
    [InlineData(true, 2, 3, 4)]     // charging in a later round: only the charge bonus
    [InlineData(false, 1, 3, 4)]    // not charging: only the unit's own, first round
    [InlineData(false, 2, 3, 3)]    // neither
    public void TuskerChargeAppliesInAnyChargingTurn(
        bool charging, int round, int baseStrength, int expected)
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(
            s: baseStrength, specialRules: "Mighty Blow (1), Tusker Charge"));

        Assert.Equal(expected, unit.EffectiveStrength(null, charging, round, 0));
    }

    [Fact]
    public async Task BoarBoyzGiveTuskerChargeToTheBoar()
    {
        var boarBoyz = await TestHelpers.FindUnitAsync("orcs-goblins", "BOAR BOYZ");

        Assert.True(boarBoyz.Mount!.SpecialRules.TuskerCharge);
        Assert.False(boarBoyz.BaseModel.SpecialRules.TuskerCharge);

        var config = new UnitConfiguration(boarBoyz) { ModelCount = 5, Frontage = 5 };
        Assert.True(config.MountSpecialRules.TuskerCharge);
        Assert.NotNull(config.MountProfile);
    }

    /// <summary>
    /// Where scoped rules end up is pinned across all shipped data, so a change to the matching
    /// shows up as a diff here rather than as a quiet shift in a simulation.
    /// </summary>
    [Fact]
    public async Task OnlyTheseUnitsArmTheirMount()
    {
        var repo = TestHelpers.Repo;
        var armed = new List<string>();

        foreach (var faction in repo.AllFactions)
        {
            foreach (var unit in await repo.GetUnitsAsync(faction.Id))
            {
                // Only rules the engine acts on.
                var mount = unit.Mount?.SpecialRules;
                if (mount is null)
                    continue;

                if (mount.MightyBlow is not null || mount.ArmourPiercing is not null
                    || mount.MultipleWounds is not null || mount.ImpactHits is not null
                    || mount.KillingBlow || mount.HeroicKillingBlow || mount.PoisonedAttacks
                    || mount.FlamingAttacks || mount.MagicalAttacks || mount.IgnoresArmourSaves
                    || mount.LightningAttacks || mount.PredatoryFighter || mount.MurderousProwess
                    || mount.MartialProwess || mount.PrimalFury || mount.TreeWhack || mount.TuskerCharge)
                {
                    armed.Add($"{faction.Id}/{unit.Name}");
                }
            }
        }

        // Joined into one string so a mismatch prints the whole list rather than an elision.
        var expected = string.Join("\n", ExpectedArmedMounts.OrderBy(s => s));
        var actual = string.Join("\n", armed.OrderBy(s => s));
        Assert.True(expected == actual,
            $"The units arming their mount changed.\nExpected:\n{expected}\nActual:\n{actual}");
    }

    /// <summary>
    /// Every unit whose mount ends up with a rule the engine acts on. Before scoped rules were
    /// routed, all of these gave the rule to the rider instead — a Daemonette poisoning with her
    /// Steed of Slaanesh's attacks — and none of them armed the mount, because AttackResolver
    /// suppressed every unit rule on a mount attack.
    /// </summary>
    private static readonly string[] ExpectedArmedMounts =
    [
        "amazons/CULCHAN RIDERS",
        "amazons/HUNTRESSES",
        "bretonnia/LADY ELISE DUCHARD",
        "daemons-of-chaos/PLEASURESEEKERS",
        "daemons-of-chaos/SEEKERS",
        "empire/DEMIGRYPH KNIGHTS",
        "estalia/KNIGHTS OF THE RAGING BULL",
        "kingdoms-of-ind/BENGAL RIDERS",
        "lizardmen/RIPPERDACTYL RIDERS",
        "orcs-goblins/BOAR BOY BIG 'UNS",
        "orcs-goblins/BOAR BOYZ",
        "orcs-goblins/GIGANTIC SPIDER RIDERS",
        "orcs-goblins/GORBAD IRONCLAW",
        "orcs-goblins/SNAGLA GROBSPIT",
        "tomb-kings/NECROPOLIS KNIGHTS",
        "warriors-of-chaos/HELLSTRIDERS",
        "warriors-of-chaos/SLICKBLADE SEEKERS",
    ];
}
