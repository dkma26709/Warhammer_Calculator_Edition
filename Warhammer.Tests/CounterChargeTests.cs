using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

/// <summary>
/// <see cref="ChargeState.BothCharged"/> models a counter-charge, which the wap-v2 Empire
/// State Troops action resolves "as if it had just declared a charge" — so both units count as
/// charging and both collect the charge bonus, impact hits and any charge-conditional weapon
/// modifier. Neither counts as having held, which is what spear and pike bonuses require.
/// </summary>
public class CounterChargeTests
{
    // ─── The context predicates ────────────────────────────────────

    [Theory]
    [InlineData(ChargeState.Neither, false, false)]
    [InlineData(ChargeState.AttackerCharged, true, false)]
    [InlineData(ChargeState.DefenderCharged, false, true)]
    [InlineData(ChargeState.BothCharged, true, true)]
    public void BothSidesChargeOnACounterCharge(ChargeState charge, bool attacker, bool defender)
    {
        var context = new CombatContext { Charge = charge };

        Assert.Equal(attacker, context.IsCharging(isAttackerSide: true));
        Assert.Equal(defender, context.IsCharging(isAttackerSide: false));
    }

    [Theory]
    [InlineData(ChargeState.Neither, false, false)]
    [InlineData(ChargeState.AttackerCharged, false, true)]
    [InlineData(ChargeState.DefenderCharged, true, false)]
    // A counter-charging unit moved, so neither side held — the spear and pike bonuses that
    // require a Hold or Stand & Shoot reaction are off for both.
    [InlineData(ChargeState.BothCharged, false, false)]
    public void NeitherSideHoldsOnACounterCharge(ChargeState charge, bool attacker, bool defender)
    {
        var context = new CombatContext { Charge = charge };

        Assert.Equal(attacker, context.IsHoldingAgainstCharge(isAttackerSide: true));
        Assert.Equal(defender, context.IsHoldingAgainstCharge(isAttackerSide: false));
    }

    // ─── Through the engine ────────────────────────────────────────

    [Fact]
    public async Task BothSidesTakeTheChargeCombatResBonus()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var engine = new CombatEngine(repo);
        var (attacker, defender) = await Infantry(repo);

        double Charge(ChargeState state)
        {
            var result = engine.Simulate(attacker, defender,
                Context(state), iterations: 4000, seed: 21);
            return result.AvgAttackerBreakdown.ChargeBonus + result.AvgDefenderBreakdown.ChargeBonus;
        }

        Assert.Equal(0, Charge(ChargeState.Neither));
        Assert.Equal(1, Charge(ChargeState.AttackerCharged));
        Assert.Equal(1, Charge(ChargeState.DefenderCharged));
        Assert.Equal(2, Charge(ChargeState.BothCharged));
    }

    [Fact]
    public async Task ACounterChargeCancelsOutOnCombatResolution()
    {
        // Two identical units: a mutual charge should leave the exchange level, where a
        // one-sided charge tilts it. This is the point of having the option.
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var engine = new CombatEngine(repo);
        var (attacker, defender) = await Infantry(repo);

        double Margin(ChargeState state)
        {
            var r = engine.Simulate(attacker, defender, Context(state), iterations: 8000, seed: 33);
            return r.AvgAttackerCombatRes - r.AvgDefenderCombatRes;
        }

        double neither = Margin(ChargeState.Neither);
        double oneSided = Margin(ChargeState.AttackerCharged);
        double mutual = Margin(ChargeState.BothCharged);

        Assert.True(oneSided > neither + 0.8, $"a one-sided charge should tilt the margin ({neither:F2} -> {oneSided:F2})");
        Assert.Equal(neither, mutual, 0.3);
    }

    [Fact]
    public async Task BothSidesDeliverImpactHitsOnACounterCharge()
    {
        // wap-v2 cavalry carry Impact Hits from the troop type, so a mutual charge should
        // produce roughly the sum of the two one-sided charges' impact wounds.
        var repo = TestHelpers.RepoFor(GameSystems.WapV2);
        var resolver = new CombatResolver(repo);
        var knights = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Bretonnia))
            .First(u => u.Name == "Knight of the Realm")) { ModelCount = 10, Frontage = 5 };
        var otherKnights = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.Bretonnia))
            .First(u => u.Name == "Knight of the Realm")) { ModelCount = 10, Frontage = 5 };

        (double Att, double Def) Round(ChargeState state)
        {
            var context = Context(state);
            var groups = resolver.PrepareAttackGroups(knights, otherKnights, context);
            long a = 0, d = 0;
            var rng = new Random(77);
            for (int i = 0; i < 3000; i++)
            {
                var r = resolver.ResolveRound(knights, otherKnights, context, groups, rng);
                a += r.AttackerWoundsDealt;
                d += r.DefenderWoundsDealt;
            }
            return (a / 3000.0, d / 3000.0);
        }

        var attackerOnly = Round(ChargeState.AttackerCharged);
        var defenderOnly = Round(ChargeState.DefenderCharged);
        var both = Round(ChargeState.BothCharged);

        // Each side hits at least as hard as it did when only the other side charged.
        Assert.True(both.Att > defenderOnly.Att, "the attacker should gain from charging too");
        Assert.True(both.Def > attackerOnly.Def, "the defender should gain from charging too");
    }

    [Fact]
    public async Task ASpearLosesItsHeldBonusWhenItCounterCharges()
    {
        // wap-v3's spear gates Mighty Blow on holding against the charge. A unit that
        // counter-charges did not hold, so it does not get it.
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var spearmen = new UnitConfiguration((await repo.GetUnitsAsync(FactionKey.TheEmpire))
            .First(u => u.Name == "Spearmen")) { ModelCount = 20, Frontage = 5 };
        var spear = spearmen.GetSelectedResolvedWeapon();

        Assert.NotNull(spear);
        Assert.Equal(WeaponCondition.WhenBeingCharged, spear.Rules.MightyBlowCondition);

        bool HoldsFor(ChargeState state)
        {
            var context = Context(state);
            return ParsedWeaponRules.Holds(spear.Rules.MightyBlowCondition,
                context.IsCharging(isAttackerSide: true),
                context.IsHoldingAgainstCharge(isAttackerSide: true),
                context.CombatRound);
        }

        Assert.True(HoldsFor(ChargeState.DefenderCharged), "held against a charge — bonus applies");
        Assert.False(HoldsFor(ChargeState.BothCharged), "counter-charged, so it did not hold");
        Assert.False(HoldsFor(ChargeState.Neither));
    }

    [Fact]
    public async Task ACounterChargeLapsesAfterTheFirstRound()
    {
        var repo = TestHelpers.RepoFor(GameSystems.WapV3);
        var resolver = new CombatResolver(repo);
        var (attacker, defender) = await Infantry(repo);

        var sets = resolver.PrepareRoundGroups(attacker, defender, Context(ChargeState.BothCharged), maxRounds: 3);

        // Round two onward is built with nobody charging, same as any other charge state.
        Assert.NotSame(sets.For(1), sets.For(2));
        Assert.Same(sets.For(2), sets.For(3));
    }

    private static CombatContext Context(ChargeState state) => new()
    {
        Charge = state,
        CombatRound = 1,
        AttackerFactionKey = "the-empire",
        DefenderFactionKey = "the-empire",
    };

    private static async Task<(UnitConfiguration Attacker, UnitConfiguration Defender)> Infantry(
        Warhammer.Services.GameDataRepository repo)
    {
        var units = await repo.GetUnitsAsync(FactionKey.TheEmpire);
        var entry = units.First(u => u.Name == "Halberdiers");
        return (new UnitConfiguration(entry) { ModelCount = 20, Frontage = 5 },
                new UnitConfiguration(entry) { ModelCount = 20, Frontage = 5 });
    }
}
