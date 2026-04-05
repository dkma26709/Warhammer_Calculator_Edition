using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Tests;

public class AttackResolverTests
{
    private const int Iterations = 2_000;

    private static AttackResult RunSingle(
        IGameDataRepository repo, AttackResolver resolver,
        UnitConfiguration attacker, UnitConfiguration defender,
        CombatContext context, Random rng,
        bool isMountAttack = false, bool isAttackerSide = true)
    {
        var weapon = repo.GetWeapon(attacker.SelectedWeapon) ?? repo.GetHandWeapon();
        int defBaseArmour = resolver.ComputeBaseArmour(defender);
        return resolver.ResolveAttacks(attacker, defender, weapon, context, isMountAttack, isAttackerSide,
            weapon.Rules, 7, rng, defBaseArmour);
    }

    // ─── Hit Rate ───────────────────────────────────────────────────

    [Fact]
    public void HitRate_WS4vWS4_Around50Percent()
    {
        // WS4 vs WS4 needs 4+ to hit = 50%
        // 5 attacks per iteration (front rank). Expected hit rate ≈ 0.50
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // 10 attacks per iteration (5 front + 5 support with 1A each)
        double hitRate = agg.HitRate(10);
        TestHelpers.AssertRate(hitRate, 0.45, 0.55, "WS4 vs WS4 hit rate");
    }

    [Fact]
    public void HitRate_WS2vWS6_Need5Plus()
    {
        // WS2 vs WS6: defender > 2× attacker WS → need 5+ = 33.3%
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 2, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 6, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        double hitRate = agg.HitRate(10);
        TestHelpers.AssertRate(hitRate, 0.28, 0.39, "WS2 vs WS6 hit rate (need 5+)");
    }

    // ─── Wound Rate ─────────────────────────────────────────────────

    [Fact]
    public void WoundRate_S3vT3_Around50Percent()
    {
        // S3 vs T3 needs 4+ to wound = 50%
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 3)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // WS10 vs WS1 = 3+ to hit ≈ 66.7%. Then S3 vs T3 = 4+ ≈ 50%.
        // Expected wounds/hit ≈ 0.50
        double woundsPerHit = agg.TotalHits > 0 ? (double)agg.TotalWounds / agg.TotalHits : 0;
        TestHelpers.AssertRate(woundsPerHit, 0.44, 0.56, "S3 vs T3 wound rate per hit");
    }

    // ─── Killing Blow ───────────────────────────────────────────────

    [Fact]
    public void KillingBlow_VsInfantry_TriggersOnNatural6ToWound()
    {
        // KB triggers on natural 6 to wound vs Infantry. With enough iterations,
        // KB should be ~16.7% of successful wounds (the 6s that wounded).
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2, specialRules: "Killing Blow"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, w: 1))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        Assert.True(agg.TotalKBWounds > 0, "KB should trigger over many iterations");
        // KB wounds should be a meaningful fraction of total wounds
        double kbFraction = (double)agg.TotalKBWounds / agg.TotalWounds;
        TestHelpers.AssertRate(kbFraction, 0.05, 0.40, "KB fraction of wounds");
    }

    [Fact]
    public void KillingBlow_VsMonster_NeverTriggers()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 3, specialRules: "Killing Blow"))
                { ModelCount = 5, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(troopType: "Monster", ws: 3, t: 5, w: 4))
                { ModelCount = 1, Frontage = 1 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        Assert.Equal(0, agg.TotalKBWounds);
    }

    // ─── Ward Save ──────────────────────────────────────────────────

    [Fact]
    public void WardSave4Plus_BlocksAboutHalfOfWounds()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var withWard = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, specialRules: "Ward save (4+)"))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var noWard = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // Ward (4+) blocks 50% → wounds with ward should be ~50% of wounds without
        double ratio = (double)withWard.TotalUnsaved / noWard.TotalUnsaved;
        TestHelpers.AssertRate(ratio, 0.35, 0.65, "Ward save (4+) wound reduction ratio");
    }

    // ─── Flaming vs Regeneration ────────────────────────────────────

    [Fact]
    public void FlamingAttacks_NegatesRegeneration()
    {
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var flaming = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2, specialRules: "Flaming Attacks"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, specialRules: "Regeneration"))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var normal = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, specialRules: "Regeneration"))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // Flaming negates Regen (4+), so flaming should deal ~2x the wounds
        TestHelpers.AssertGreaterByRatio(flaming.TotalUnsaved, normal.TotalUnsaved, 1.3,
            "Flaming vs Regen should deal significantly more wounds");
    }

    // ─── Hatred ─────────────────────────────────────────────────────

    [Fact]
    public void Hatred_RerollMisses_IncreasesHitsByAbout33Percent()
    {
        // Hatred rerolls missed hits. WS3 vs WS4 = need 5+ (33% base).
        // With reroll: 1 - (2/3)^2 ≈ 55.6%. So hatred should boost hits by ~67%.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var withHatred = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, s: 3, a: 1, specialRules: "Hatred"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var noHatred = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, s: 3, a: 1))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // Hatred should boost hits by at least 30%
        TestHelpers.AssertGreaterByRatio(withHatred.TotalHits, noHatred.TotalHits, 1.30,
            "Hatred reroll should increase hits significantly");
    }

    // ─── Armour Saves ───────────────────────────────────────────────

    [Fact]
    public void HeavyArmourAndShield_ReducesWoundsByAboutHalf()
    {
        // Heavy armour (4+) + shield (+1) = 3+ save vs S3 (no modifier).
        // 3+ save blocks 4/6 ≈ 66.7%. So armoured wounds ≈ 33% of naked.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var armoured = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 2)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3, equipment: "Hand weapon, heavy armour, shield"))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var naked = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 2)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // Heavy armour + shield at S3 should block most wounds
        double ratio = (double)armoured.TotalUnsaved / naked.TotalUnsaved;
        TestHelpers.AssertRate(ratio, 0.15, 0.55, "Heavy armour + shield wound reduction");
    }

    // ─── Impact Hits ────────────────────────────────────────────────

    [Fact]
    public void ImpactHits_OnCharge_DealsExpectedWounds()
    {
        // Impact Hits (1) × 5 front rank models = 5 auto-hits.
        // S3 vs T3 = 4+ to wound = 50%. So ~2.5 wounds per iteration (before saves).
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        int totalWounds = 0;
        for (int seed = 0; seed < Iterations; seed++)
        {
            var rng = new Random(seed);
            var charger = new UnitConfiguration(TestHelpers.MakeUnit(troopType: "Cavalry", s: 3, specialRules: "Impact Hits (1)"))
                { ModelCount = 5, Frontage = 5 };
            var target = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 10, Frontage = 5 };
            var context = new CombatContext { Charge = ChargeState.AttackerCharged, CombatRound = 1 };
            int targetBaseArmour = resolver.ComputeBaseArmour(target);
            totalWounds += resolver.ResolveImpactHits(charger, target, context, rng, targetBaseArmour);
        }

        double avg = (double)totalWounds / Iterations;
        // 5 hits × 50% wound × 100% unsaved (no armour) ≈ 2.5
        TestHelpers.AssertRate(avg, 1.5, 3.5, "Impact hits avg wounds");
    }

    // ─── Predatory Fighter ──────────────────────────────────────────

    [Fact]
    public void PredatoryFighter_GeneratesExtraHits_OnNatural6()
    {
        // PF generates an extra attack on each natural 6 to hit.
        // Base hit rate WS4v3 = 3+ (66.7%). Of those, 1/6 are natural 6s → extra attacks.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var withPF = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2, specialRules: "Predatory Fighter"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var noPF = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 4, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 3))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // PF should increase hits by ~16.7% (one extra attack per natural 6)
        TestHelpers.AssertGreaterByRatio(withPF.TotalHits, noPF.TotalHits, 1.08,
            "Predatory Fighter should noticeably increase hits");
    }

    // ─── Murderous Prowess ──────────────────────────────────────────

    [Fact]
    public void MurderousProwess_RerollOnesToWound_IncreasesWounds()
    {
        // MP rerolls 1s to wound. S3 vs T4 = need 5+ (33.3%).
        // 1s are 16.7% of rolls; rerolling them: +16.7% × 33.3% ≈ +5.6% wounds.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var withMP = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 2, specialRules: "Murderous Prowess"))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 4))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var noMP = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 2))
                { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 3, t: 4))
                { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        TestHelpers.AssertGreaterByRatio(withMP.TotalWounds, noMP.TotalWounds, 1.03,
            "Murderous Prowess should increase wounds");
    }

    // ─── Tier 3 Roll Modifiers ──────────────────────────────────────

    [Fact]
    public void ToHitModifier_Minus1_IncreasesHitRate()
    {
        // WS4 vs WS4 = need 4+ (50%). With ToHitModifier -1, need 3+ (66.7%).
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var baseline = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var modified = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { ToHitModifier = -1 });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        TestHelpers.AssertGreaterByRatio(modified.TotalHits, baseline.TotalHits, 1.25,
            "ToHitModifier -1 should boost hit rate from ~50% to ~67%");
    }

    [Fact]
    public void ToHitModifier_Plus1_DecreasesHitRate()
    {
        // WS4 vs WS4 = need 4+ (50%). With ToHitModifier +1, need 5+ (33.3%).
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var baseline = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var modified = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { ToHitModifier = 1 });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        TestHelpers.AssertGreaterByRatio(baseline.TotalHits, modified.TotalHits, 1.35,
            "ToHitModifier +1 should reduce hit rate from ~50% to ~33%");
    }

    [Fact]
    public void FixedToHit_2Plus_HitsAlmost_Always()
    {
        // Fixed 2+ = 83.3% hit rate regardless of WS matchup.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { FixedToHit = 2 });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // WS1 vs WS10 would normally need 5+ (33.3%). Fixed 2+ = 83.3%.
        double hitRate = agg.HitRate(10);
        TestHelpers.AssertRate(hitRate, 0.78, 0.88, "FixedToHit 2+ hit rate");
    }

    [Fact]
    public void ToWoundModifier_Minus1_IncreasesWoundRate()
    {
        // S3 vs T3 = need 4+ (50%). With ToWoundModifier -1, need 3+ (66.7%).
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var baseline = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 3)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var modified = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { ToWoundModifier = -1 });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 3)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        TestHelpers.AssertGreaterByRatio(modified.TotalWounds, baseline.TotalWounds, 1.25,
            "ToWoundModifier -1 should boost wound rate from ~50% to ~67%");
    }

    [Fact]
    public void FixedToWound_2Plus_WoundsAlmostAlways()
    {
        // Fixed 2+ = 83.3% wound rate regardless of S vs T.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var agg = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 1, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { FixedToWound = 2 });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 10)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // S1 vs T10 would normally need 6+ (16.7%). Fixed 2+ = 83.3%.
        double woundsPerHit = agg.TotalHits > 0 ? (double)agg.TotalWounds / agg.TotalHits : 0;
        TestHelpers.AssertRate(woundsPerHit, 0.78, 0.88, "FixedToWound 2+ wound rate per hit");
    }

    [Fact]
    public void RerollFailedToHit_IncreasesHitRate()
    {
        // WS4 vs WS4 = need 4+ (50%). With reroll: 1 - (0.5)^2 = 75%.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var baseline = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var rerolling = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { RerollFailedToHit = true });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4, t: 3)) { ModelCount = 10, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        // Rerolling misses at 50% base → 75% effective. ~50% increase in hits.
        TestHelpers.AssertGreaterByRatio(rerolling.TotalHits, baseline.TotalHits, 1.35,
            "RerollFailedToHit should boost hits significantly");
    }

    [Fact]
    public void RerollFailedToWound_IncreasesWoundRate()
    {
        // S3 vs T3 = need 4+ (50%). With reroll: 1 - (0.5)^2 = 75%.
        var repo = TestHelpers.CreateRepository();
        var resolver = new AttackResolver(repo);

        var baseline = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 3)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        var rerolling = TestHelpers.RunAttackTrials(Iterations, rng =>
        {
            var att = new UnitConfiguration(TestHelpers.MakeUnit(ws: 10, s: 3, a: 1)) { ModelCount = 10, Frontage = 5 };
            att.Modifiers.Add(new UnitModifier { RerollFailedToWound = true });
            var def = new UnitConfiguration(TestHelpers.MakeUnit(ws: 1, t: 3)) { ModelCount = 20, Frontage = 5 };
            return RunSingle(repo, resolver, att, def, new CombatContext { CombatRound = 1 }, rng);
        });

        TestHelpers.AssertGreaterByRatio(rerolling.TotalWounds, baseline.TotalWounds, 1.35,
            "RerollFailedToWound should boost wounds significantly");
    }
}
