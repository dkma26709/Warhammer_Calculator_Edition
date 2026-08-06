using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Rules the army books scope to one model in a unit — "Mighty Blow (2) (War Boars only)". They
/// used to be mishandled in both directions. The two-parenthetical form reached no arm at all and
/// was dropped: 33 instances in wap-v2 and 31 in wap-v3. The one-parenthetical form parsed cleanly
/// and was applied to the whole unit: 54 and 53 instances, arming every Daemonette with its Steed
/// of Slaanesh's poison. 60 wap-v2 and 59 wap-v3 units were affected one way or the other.
/// </summary>
public class ScopedRuleTests
{
    private static async Task<Unit> Find(string system, FactionKey faction, string name)
    {
        var repo = TestHelpers.RepoFor(system);
        return (await repo.GetUnitsAsync(faction)).First(u => u.Name == name);
    }

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

    // ─── Routing onto the right model ──────────────────────────────

    /// <summary>
    /// The boar's Mighty Blow is the boar's, not the Orc's. wap-v2 only: v3 replaced the boar's
    /// "Mighty Blow (2) (War Boars only)" with Tusker Charge, and dropped the Orc's own bonus.
    /// </summary>
    [Fact]
    public async Task BoarBoyzGiveTheirMightyBlowToTheBoar()
    {
        var boarBoyz = await Find(GameSystems.WapV2, FactionKey.OrcsAndGoblins, "Boar Boyz");

        Assert.Equal(1, boarBoyz.BaseModel.SpecialRules.MightyBlow);
        Assert.Equal(2, boarBoyz.Mount!.SpecialRules.MightyBlow);
    }

    /// <summary>
    /// Ripperdactyl Riders were the clearest over-application: the Skink Brave was getting the
    /// beast's Killing Blow, Frenzy and Armour Piercing.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task RipperdactylRulesBelongToTheRipperdactyl(string system)
    {
        var riders = await Find(system, FactionKey.Lizardmen, "Ripperdactyl Riders");

        Assert.False(riders.BaseModel.SpecialRules.KillingBlow);
        Assert.Null(riders.BaseModel.SpecialRules.ArmourPiercing);
        Assert.True(riders.Mount!.SpecialRules.KillingBlow);
        Assert.Equal(1, riders.Mount.SpecialRules.ArmourPiercing);
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task SeekersGiveTheirPoisonToTheSteed(string system)
    {
        var seekers = await Find(system, FactionKey.DaemonsOfChaos, "Seekers");

        Assert.False(seekers.BaseModel.SpecialRules.PoisonedAttacks);
        Assert.True(seekers.Mount!.SpecialRules.PoisonedAttacks);
    }

    /// <summary>
    /// A scope naming the unit's own fighting profile belongs to the unit — this is the direction
    /// that was silently dropped rather than over-applied.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task ASalamandersOwnNaturalArmourApplies(string system)
    {
        var salamander = await Find(system, FactionKey.Lizardmen, "Salamander");

        // "Natural Armour (5+) (Salamander only)" is the Salamander's; the Skink Handler's 6+ is
        // not, and the handler is not fielded as a fighting model.
        Assert.Equal(5, salamander.BaseModel.SpecialRules.NaturalArmour);
    }

    /// <summary>
    /// A champion-only rule goes nowhere. The engine fields one profile for the rank and file, so
    /// there is no model to put a Slave Master's Murderous Prowess on — and giving it to all
    /// twenty slaves, which is what happened before, is worse than leaving it out.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task AChampionOnlyRuleIsNotGivenToTheWholeUnit(string system)
    {
        var slaves = await Find(system, FactionKey.DarkElves, "Slaves");

        Assert.False(slaves.BaseModel.SpecialRules.MurderousProwess);
        Assert.False(slaves.BaseModel.SpecialRules.Hatred);
        Assert.False(slaves.BaseModel.SpecialRules.ElvenGrace);
    }

    // ─── It reaches the combat maths ───────────────────────────────

    /// <summary>
    /// The boar's Mighty Blow has to raise the mount's Strength, which read straight off the
    /// profile before. A War Boar is S3, so first-round attacks land at S5.
    /// </summary>
    [Fact]
    public async Task TheBoarsMightyBlowRaisesTheMountsStrength()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV2);
        var boarBoyz = new UnitConfiguration(
            (await repo.GetUnitsAsync(FactionKey.OrcsAndGoblins)).First(u => u.Name == "Boar Boyz"))
        { ModelCount = 5, Frontage = 5 };

        Assert.Equal(2, boarBoyz.MountSpecialRules.MightyBlow);
        Assert.Equal(3, boarBoyz.MountProfile!.S);
    }

    /// <summary>
    /// wap-v3 replaced the boar's "Mighty Blow (2) (War Boars only)" with Tusker Charge — "Models
    /// with this special rule have the Mighty Blow (1) special rule in any turn that they charge"
    /// — and scoped that to the boar too. With no arm for it, v3 Boar Boyz had no boar bonus at all.
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
    public async Task V3BoarBoyzGiveTuskerChargeToTheBoar()
    {
        var boarBoyz = await Find(GameSystems.WapV3, FactionKey.OrcsAndGoblins, "Boar Boyz");

        Assert.True(boarBoyz.Mount!.SpecialRules.TuskerCharge);
        Assert.False(boarBoyz.BaseModel.SpecialRules.TuskerCharge);
    }

    /// <summary>
    /// Where scoped rules end up is pinned across all shipped data, so a change to the matching
    /// shows up as a diff here rather than as a quiet shift in a simulation.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task OnlyTheseUnitsArmTheirMount(string system)
    {
        var repo = TestHelpers.RepoFor(system);
        var armed = new List<string>();

        foreach (var faction in Enum.GetValues<FactionKey>())
        {
            IReadOnlyList<Unit> units;
            try
            {
                units = await repo.GetUnitsAsync(faction);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var unit in units)
            {
                // Only rules the engine acts on. A scoped rule with no arm yet — wap-v3's Tusker
                // Charge, say — reaches the mount but changes nothing, so it does not belong here.
                var mount = unit.Mount?.SpecialRules;
                if (mount is null)
                    continue;

                if (mount.MightyBlow is not null || mount.ArmourPiercing is not null
                    || mount.MultipleWounds is not null || mount.ImpactHits is not null
                    || mount.KillingBlow || mount.HeroicKillingBlow || mount.PoisonedAttacks
                    || mount.FlamingAttacks || mount.MagicalAttacks || mount.IgnoresArmourSaves
                    || mount.LightningAttacks || mount.PredatoryFighter || mount.MurderousProwess
                    || mount.MartialProwess || mount.PrimalFury || mount.TreeWhack)
                {
                    armed.Add($"{faction}/{unit.Name}");
                }
            }
        }

        // Joined into one string so a mismatch prints the whole list rather than an elision.
        Assert.Equal(
            string.Join("\n", ExpectedArmedMounts[system].OrderBy(s => s)),
            string.Join("\n", armed.OrderBy(s => s)));
    }

    /// <summary>
    /// Every unit whose mount ends up with a rule the engine acts on. Before scoped rules were
    /// routed, all of these gave the rule to the rider instead — a Daemonette poisoning with her
    /// Steed of Slaanesh's attacks — and none of them armed the mount, because AttackResolver
    /// suppressed every unit rule on a mount attack.
    /// </summary>
    private static readonly Dictionary<string, string[]> ExpectedArmedMounts = new()
    {
        [GameSystems.WapV2] =
        [
            "Bretonnia/Lady Elise Duchard",
            "DaemonsOfChaos/Pleasureseekers",
            "DaemonsOfChaos/Seekers",
            "Lizardmen/Ripperdactyl Riders",
            "OrcsAndGoblins/Big 'un Boar Boyz",
            "OrcsAndGoblins/Boar Boyz",
            "OrcsAndGoblins/Gorbad Ironclaw",
            "OrcsAndGoblins/Gore-gruntas",
            "TombKings/Necropolis Knights",
            "WarriorsOfChaos/Hellstriders",
            "WarriorsOfChaos/Kayzk the Befouled",
            "WarriorsOfChaos/Pusgoyle Blightlords",
            "WarriorsOfChaos/Rot Knights",
            "WarriorsOfChaos/Slickblade Seekers",
        ],
        [GameSystems.WapV3] =
        [
            "Bretonnia/Lady Elise Duchard",
            "DaemonsOfChaos/Pleasureseekers",
            "DaemonsOfChaos/Seekers",
            "Lizardmen/Ripperdactyl Riders",
            "OrcsAndGoblins/Gigantic Spider Riders",
            "OrcsAndGoblins/Snagla Grobspit",
            "OrcsAndGoblins/Spider Riders",
            "TheEmpire/Demigryph Knights",
            "TombKings/Necropolis Knights",
            "WarriorsOfChaos/Hellstriders",
            "WarriorsOfChaos/Pusgoyle Blightlords",
            "WarriorsOfChaos/Rot Knights",
            "WarriorsOfChaos/Slickblade Seekers",
        ],
    };
}
