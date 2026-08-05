using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Weapon modifiers that hold only on a charge, or only in the first round, must stop applying
/// when their condition lapses. Attack groups are pre-built for speed, so the engine has to
/// rebuild them when the round context changes or these conditions freeze at their round-1
/// values for the whole combat.
/// </summary>
public class RoundDependentModifierTests
{

    /// <summary>
    /// wap-v3's spear grants +1 Initiative in the first round of close combat only. Strike
    /// order comes from the pre-built groups, so if those aren't rebuilt the bonus persists.
    /// </summary>
    [Fact]
    public void AFirstRoundInitiativeBonusDoesNotSurviveIntoLaterRounds()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var spear = repo.GetWeapon(CloseCombatWeaponKey.Spear);

        Assert.NotNull(spear);
        Assert.Equal(WeaponCondition.FirstRound, spear.Rules.InitiativeModCondition);
        Assert.Equal(1, spear.Rules.InitiativeMod);

        Assert.Equal(4, spear.Rules.ApplyToInitiative(3, false, false, combatRound: 1));
        Assert.Equal(3, spear.Rules.ApplyToInitiative(3, false, false, combatRound: 2));
    }

    /// <summary>
    /// The engine builds one group set for the charge round and one for the rounds after it.
    /// Both must reflect their own context — this is the regression the pre-built groups hid.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task StrikeOrderIsRebuiltWhenTheRoundContextChanges(string systemKey)
    {
        var repo = TestHelpers.RepoFor(systemKey);
        var resolver = new CombatResolver(repo);

        // Spearmen (I3, spear) against Saurus Warriors (I1, hand weapon). A spear's Initiative
        // bonus is first-round-only in wap-v3 and unconditional in wap-v2, so the two systems
        // should disagree about round 2 and agree about round 1.
        var spearmen = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
            .First(u => u.Name == "Spearmen")) { ModelCount = 20, Frontage = 5 };
        var saurus = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Lizardmen))
            .First(u => u.Name == "Saurus Warriors")) { ModelCount = 20, Frontage = 5 };

        var round1 = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var round2 = round1 with { CombatRound = 2 };

        int SpearmenPriority(CombatContext context) => resolver
            .PrepareAttackGroups(spearmen, saurus, context)
            .First(g => g.IsAttackerSide && !g.IsMountAttack)
            .Priority;

        int opening = SpearmenPriority(round1);
        int later = SpearmenPriority(round2);

        if (systemKey == GameSystems.WapV3)
            Assert.True(opening > later,
                $"wap-v3 spear should lose its Initiative bonus after round 1 ({opening} then {later})");
        else
            Assert.Equal(opening, later);
    }

    /// <summary>
    /// A lance's Mighty Blow applies only on the turn it charges. The charge round and the
    /// rounds after it must therefore resolve at different Strength.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task ALanceStopsHittingAtChargeStrengthAfterTheChargeRound(string systemKey)
    {
        var repo = TestHelpers.RepoFor(systemKey);
        var knights = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Bretonnia))
            .First(u => u.Name == "Knight of the Realm")) { ModelCount = 10, Frontage = 5 };
        var lance = knights.GetSelectedResolvedWeapon();

        Assert.NotNull(lance);
        Assert.Equal(WeaponCondition.WhenCharging, lance.Rules.MightyBlowCondition);

        int Strength(bool charging, int round)
        {
            int mb = ParsedWeaponRules.Holds(lance.Rules.MightyBlowCondition, charging, false, round)
                ? lance.Rules.MightyBlow ?? 0
                : 0;
            return knights.EffectiveStrength(lance, charging, round, mb);
        }

        Assert.Equal(5, Strength(charging: true, round: 1));    // S3 + Mighty Blow (2)
        Assert.Equal(3, Strength(charging: false, round: 1));   // no charge, no lance
        Assert.Equal(3, Strength(charging: false, round: 2));
    }

    /// <summary>
    /// The seam the engine depends on: the set for the charge round and the set for later
    /// rounds must be built from their own contexts, and every round past the first shares one.
    /// </summary>
    [Fact]
    public async Task LaterRoundsGetTheirOwnGroupSet()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var resolver = new CombatResolver(repo);

        // Empire Spearmen (I3, spear) against Bloodletters (I4). wap-v3's spear grants +1
        // Initiative in the first round only, so the strike order genuinely flips: round one
        // is a 4-vs-4 tie resolved simultaneously, round two has the Bloodletters going first.
        var spearmen = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
            .First(u => u.Name == "Spearmen")) { ModelCount = 20, Frontage = 5 };
        var bloodletters = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.DaemonsOfChaos))
            .First(u => u.Name == "Bloodletters")) { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var sets = resolver.PrepareRoundGroups(spearmen, bloodletters, context, maxRounds: 4);

        static string Order(List<CombatResolver.AttackGroup> groups) => string.Join(",",
            groups.Select(g => $"{(g.IsAttackerSide ? "attacker" : "defender")}:{g.Priority}"));

        Assert.Equal("attacker:4,defender:4", Order(sets.For(1)));
        Assert.Equal("defender:4,attacker:3", Order(sets.For(2)));

        // Rounds three onward are the same context as round two, so they share its set.
        Assert.Same(sets.For(2), sets.For(3));
        Assert.Same(sets.For(2), sets.For(9));
        Assert.NotSame(sets.For(1), sets.For(2));
    }

    [Fact]
    public async Task ASingleRoundRunNeedsOnlyTheOpeningSet()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var resolver = new CombatResolver(repo);

        var spearmen = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
            .First(u => u.Name == "Spearmen")) { ModelCount = 20, Frontage = 5 };
        var saurus = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Lizardmen))
            .First(u => u.Name == "Saurus Warriors")) { ModelCount = 20, Frontage = 5 };

        var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };
        var sets = resolver.PrepareRoundGroups(spearmen, saurus, context, maxRounds: 1);

        Assert.Same(sets.Opening, sets.Later);
    }

    /// <summary>
    /// End to end: a charge must not still be in effect in round two.
    /// </summary>
    [Fact]
    public async Task AChargeBonusDoesNotInflateLaterRoundsOfAMultiRoundRun()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var engine = new CombatEngine(repo);

        var knights = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Bretonnia))
            .First(u => u.Name == "Knight of the Realm")) { ModelCount = 10, Frontage = 5 };
        var halberdiers = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
            .First(u => u.Name == "Halberdiers")) { ModelCount = 25, Frontage = 5 };

        var charged = new CombatContext
        {
            Charge = ChargeState.AttackerCharged, CombatRound = 1,
            AttackerFactionKey = "bretonnia", DefenderFactionKey = "the-empire"
        };
        var neither = charged with { Charge = ChargeState.Neither };

        var withCharge = engine.SimulateMultiRound(knights, halberdiers, charged, 3, 4000, seed: 3);
        var without = engine.SimulateMultiRound(knights, halberdiers, neither, 3, 4000, seed: 3);

        // Round 1 differs — the charge is real.
        Assert.True(withCharge.RoundSummaries[0].AvgAttackerWounds
            > without.RoundSummaries[0].AvgAttackerWounds + 0.3,
            "the charge round should be measurably stronger");

        // Round 2 must not: by then neither run is charging, so the only difference left is
        // the casualties each took in round 1.
        double chargedR2 = withCharge.RoundSummaries[1].AvgAttackerWounds;
        double neitherR2 = without.RoundSummaries[1].AvgAttackerWounds;
        Assert.True(Math.Abs(chargedR2 - neitherR2) < 0.5,
            $"round 2 should not carry the charge bonus (charged {chargedR2:F2} vs {neitherR2:F2})");
    }
}
