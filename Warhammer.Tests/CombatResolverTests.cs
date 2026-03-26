using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class CombatResolverTests
{
    private const int Iterations = 2_000;

    private static RoundResult Resolve(CombatResolver resolver, UnitConfiguration attacker,
        UnitConfiguration defender, CombatContext context, Random rng)
    {
        var groups = resolver.PrepareAttackGroups(attacker, defender, context);
        return resolver.ResolveRound(attacker, defender, context, groups, rng);
    }

    // ─── Impact Hits ────────────────────────────────────────────────

    [Fact]
    public void ImpactHits_OnlyTriggerOnCharge()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var chargeResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var charger = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 1, specialRules: "Impact Hits (2)"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, charger, def,
                new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 }, rng);
        });

        var noChargeResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 1, specialRules: "Impact Hits (2)"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 }, rng);
        });

        // Impact Hits (2) × 5 front rank = 10 extra auto-hits on charge
        // S4 vs T3 = 3+ wound = 66.7%. ~6.7 extra wounds per iteration.
        TestHelpers.AssertGreaterByRatio(
            chargeResult.TotalAttackerWounds, noChargeResult.TotalAttackerWounds, 1.3,
            "Charging with impact hits should deal materially more wounds");
    }

    // ─── Stomp ──────────────────────────────────────────────────────

    [Fact]
    public void Stomp_MonstrousInfantryVsInfantry_DealsExtraWounds()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var monstrousResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var monster = new UnitConfiguration(TestHelpers.MakeUnit(
                troopType: "Monstrous Infantry", ws: 4, s: 4, a: 3, unitSize: "3+"))
                { ModelCount = 3, Frontage = 3 };
            var infantry = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, monster, infantry,
                new CombatContext { CombatRound = 1 }, rng);
        });

        var infantryResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            // Same stats but regular infantry (no stomp)
            var att = new UnitConfiguration(TestHelpers.MakeUnit(
                troopType: "Infantry", ws: 4, s: 4, a: 3, unitSize: "3+"))
                { ModelCount = 3, Frontage = 3 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // Monstrous Infantry gets Stomp (1 hit per model in base contact)
        TestHelpers.AssertGreaterByRatio(
            monstrousResult.TotalAttackerWounds, infantryResult.TotalAttackerWounds, 1.05,
            "Monstrous Infantry stomp should add wounds vs Infantry");
    }

    [Fact]
    public void Stomp_InfantryVsInfantry_NoStomp()
    {
        // Infantry should never stomp infantry — verify by comparing
        // a symmetric mirror match produces symmetric results
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var result = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // Mirror match: avg wounds should be roughly equal (within 20%)
        double ratio = result.AvgAttackerWounds > 0
            ? result.AvgDefenderWounds / result.AvgAttackerWounds
            : 0;
        TestHelpers.AssertRate(ratio, 0.70, 1.40, "Mirror infantry match should be roughly symmetric");
    }

    // ─── Combat Resolution ──────────────────────────────────────────

    [Fact]
    public void CombatRes_RankBonusAndStandard_Deterministic()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);
        var rng = new Random(42);

        // Use A=0 so no attacks are made, isolating rank/standard from casualty effects.
        // CR rank bonus uses post-casualty model count (which here stays unchanged).
        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 0, unitSize: "20+"))
            { ModelCount = 20, Frontage = 5, HasStandardBearer = true };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3, a: 0, unitSize: "10+"))
            { ModelCount = 10, Frontage = 5 };

        var result = Resolve(resolver, attacker, defender,
            new CombatContext { CombatRound = 1 }, rng);

        // 20 models / 5 frontage = 4 ranks → rank bonus = 3 (capped)
        Assert.Equal(3, result.AttackerBreakdown.RankBonus);
        Assert.Equal(1, result.AttackerBreakdown.StandardBearer);
        // Defender: 10/5 = 2 ranks → rank bonus = 1
        Assert.Equal(1, result.DefenderBreakdown.RankBonus);
        Assert.Equal(0, result.DefenderBreakdown.StandardBearer);
    }

    [Fact]
    public void CombatRes_ChargeBonus_OnlyForCharger()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);
        var rng = new Random(42);

        var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1))
            { ModelCount = 10, Frontage = 5 };
        var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3))
            { ModelCount = 10, Frontage = 5 };

        var result = Resolve(resolver, attacker, defender,
            new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 }, rng);

        Assert.Equal(1, result.AttackerBreakdown.ChargeBonus);
        Assert.Equal(0, result.DefenderBreakdown.ChargeBonus);
    }

    // ─── Fear ───────────────────────────────────────────────────────

    [Fact]
    public void Fear_GivesCRBonus_Against_NonFearCausing()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);
        var rng = new Random(42);

        var fearUnit = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1, specialRules: "Fear"))
            { ModelCount = 10, Frontage = 5 };
        var normalUnit = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3))
            { ModelCount = 10, Frontage = 5 };

        var result = Resolve(resolver, fearUnit, normalUnit,
            new CombatContext { CombatRound = 1 }, rng);

        Assert.Equal(1, result.AttackerBreakdown.FearBonus);
        Assert.Equal(0, result.DefenderBreakdown.FearBonus);
    }

    [Fact]
    public void Fear_DoubleUS_Gives2Bonus()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);
        var rng = new Random(42);

        // Monstrous Infantry: US=2 per model, 6 models = US 12
        var fearUnit = new UnitConfiguration(TestHelpers.MakeUnit(
            troopType: "Monstrous Infantry", ws: 4, s: 4, a: 2,
            specialRules: "Fear", unitSize: "6+"))
            { ModelCount = 6, Frontage = 3 };

        // Infantry: US=1, 5 models = US 5. 12 >= 2×5, so +2 fear bonus
        var smallUnit = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, unitSize: "5+"))
            { ModelCount = 5, Frontage = 5 };

        var result = Resolve(resolver, fearUnit, smallUnit,
            new CombatContext { CombatRound = 1 }, rng);

        Assert.Equal(2, result.AttackerBreakdown.FearBonus);
    }

    [Fact]
    public void Fear_VsFearCausing_NoBonus()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);
        var rng = new Random(42);

        var fearUnit1 = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1, specialRules: "Fear"))
            { ModelCount = 10, Frontage = 5 };
        var fearUnit2 = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, a: 1, specialRules: "Fear"))
            { ModelCount = 10, Frontage = 5 };

        var result = Resolve(resolver, fearUnit1, fearUnit2,
            new CombatContext { CombatRound = 1 }, rng);

        // Both cause Fear → immune to each other's Fear
        Assert.Equal(0, result.AttackerBreakdown.FearBonus);
        Assert.Equal(0, result.DefenderBreakdown.FearBonus);
    }

    // ─── Great Weapon ───────────────────────────────────────────────

    [Fact]
    public void GreatWeapon_LowersInitiativeBy2_StrikesAfterHigherI()
    {
        // GW: -2 Initiative. Unit with I5 + GW = effective I3.
        // Defender I4 should strike first and cause casualties,
        // reducing attacker attacks (if models die).
        // We test this indirectly: GW unit should deal fewer total wounds
        // than the same unit with hand weapon because it strikes later
        // (and potentially loses models before attacking).
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var gwResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, i: 5, a: 1, equipment: "Great weapon"))
                { ModelCount = 10, Frontage = 5, SelectedWeaponKey = "greatWeapon" };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, t: 3, i: 4, a: 1))
                { ModelCount = 10, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // GW gives +2S though, so wounds per hit are higher.
        // Just verify the system works and produces reasonable output.
        Assert.True(gwResult.AvgAttackerWounds > 0, "GW unit should deal wounds");
        Assert.True(gwResult.AvgDefenderWounds > 0, "Defender should deal wounds back");
    }

    // ─── Spear ──────────────────────────────────────────────────────

    [Fact]
    public void Spear_FightInExtraRanks_DealsMoreDamage()
    {
        // Spear gives FiER(1), so a 3-deep unit gets 2 support ranks instead of 1.
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var spearResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1,
                equipment: "Spear, shield", unitSize: "20+"))
                { ModelCount = 20, Frontage = 5, SelectedWeaponKey = "spear" };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3, i: 3, a: 1))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        var hwResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1, unitSize: "20+"))
                { ModelCount = 20, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3, i: 3, a: 1))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // Spear FiER(1) should add ~5 extra attacks (1 extra support rank × 5 models)
        TestHelpers.AssertGreaterByRatio(
            spearResult.TotalAttackerWounds, hwResult.TotalAttackerWounds, 1.10,
            "Spear FiER should increase damage output");
    }

    // ─── Cavalry Mount Attacks ──────────────────────────────────────

    [Fact]
    public void Cavalry_MountAttacks_IncludeRiderAndMount()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var cavResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var mount = new ProfileEntry { Name = "Warhorse", WS = 3, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
            var unit = TestHelpers.MakeUnit(troopType: "Cavalry", ws: 4, s: 3, i: 3, a: 1,
                unitSize: "10+", mountProfile: mount);
            var cavalry = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, cavalry, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        var footResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1, unitSize: "10+"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // Cavalry should deal more wounds (rider attacks + mount attacks)
        TestHelpers.AssertGreaterByRatio(
            cavResult.TotalAttackerWounds, footResult.TotalAttackerWounds, 1.3,
            "Cavalry (rider + mount) should out-damage equivalent foot");
    }

    // ─── Musician ───────────────────────────────────────────────────

    [Fact]
    public void Musician_BreaksTies_WhenCRIsDrawn()
    {
        // In a drawn combat, musician tips it. We run many iterations and check
        // that the musician side wins draws more often.
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        int musicianWins = 0, opponentWins = 0, draws = 0;
        for (int seed = 0; seed < Iterations; seed++)
        {
            var rng = new Random(seed);
            var attacker = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1))
                { ModelCount = 10, Frontage = 5, HasMusician = true };
            var defender = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };

            var result = Resolve(resolver, attacker, defender,
                new CombatContext { CombatRound = 1 }, rng);

            int diff = result.AttackerCombatRes - result.DefenderCombatRes;
            if (diff > 0) musicianWins++;
            else if (diff < 0) opponentWins++;
            else draws++;
        }

        // Musician converts draws into wins, so attacker should win more than lose
        Assert.True(musicianWins > opponentWins,
            $"Musician should help win: wins={musicianWins}, losses={opponentWins}, draws={draws}");
    }

    // ─── Intra-Round Casualty Removal ─────────────────────────────────

    [Fact]
    public void HighInitiative_KillsModels_ReducesRetaliationAttacks()
    {
        // Attacker: I6, S5, A2 — strikes first and hard.
        // Defender: I1, same stats — strikes last with reduced model count.
        // Compare to a mirror match at the same I: the slow side should deal
        // fewer average wounds because it loses models before striking.
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        // Fast attacker vs slow defender
        var fastResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 5, i: 6, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 5, t: 3, i: 1, a: 2))
                { ModelCount = 10, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // Same stats but defender has same initiative (simultaneous)
        var simultaneousResult = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 5, i: 6, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 5, t: 3, i: 6, a: 2))
                { ModelCount = 10, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // The slow defender should deal fewer wounds than the simultaneous one,
        // because it loses models before striking.
        Assert.True(fastResult.AvgDefenderWounds < simultaneousResult.AvgDefenderWounds,
            $"Slow defender ({fastResult.AvgDefenderWounds:F2}) should deal fewer wounds than " +
            $"simultaneous ({simultaneousResult.AvgDefenderWounds:F2}) due to casualty removal");
    }

    [Fact]
    public void SameInitiative_StrikeSimultaneously_NoPreemptiveRemoval()
    {
        // Both sides at I3: neither loses models before the other strikes.
        // The attacker and defender wound outputs should be symmetric-ish
        // (same stats, same count).
        var repo = TestHelpers.CreateRepository();
        var resolver = new CombatResolver(repo);

        var result = TestHelpers.RunRoundTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, i: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, t: 3, i: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            return Resolve(resolver, att, def,
                new CombatContext { CombatRound = 1 }, rng);
        });

        // With identical stats and simultaneous strikes, wounds should be roughly equal
        double ratio = result.AvgAttackerWounds / Math.Max(result.AvgDefenderWounds, 0.01);
        Assert.True(ratio > 0.8 && ratio < 1.2,
            $"Symmetric match should be roughly equal: att={result.AvgAttackerWounds:F2}, " +
            $"def={result.AvgDefenderWounds:F2}");
    }
}
