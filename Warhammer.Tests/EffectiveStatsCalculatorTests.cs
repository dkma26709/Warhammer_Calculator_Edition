using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The numbers the Matchup page states for each side. The Greatswords/Hammerers figures are the
/// ones EffectiveStatsPanel displayed before the computation moved out of the component.
/// </summary>
public class EffectiveStatsCalculatorTests
{
    private static readonly EffectiveStatsCalculator Calculator = new(TestHelpers.Repo);

    // Configured as the Matchup page does when a unit is picked: defaults untouched.
    private static async Task<UnitConfiguration> Picked(FactionKey faction, string name)
    {
        var units = await TestHelpers.Repo.GetUnitsAsync(faction);
        return new UnitConfiguration(units.First(u => u.Name == name));
    }

    private static async Task<(UnitConfiguration Greatswords, UnitConfiguration Hammerers)> Matchup()
    {
        var greatswords = await Picked(FactionKey.TheEmpire, "Greatswords");
        var hammerers = await Picked(FactionKey.Dwarfs, "Hammerers");

        // Guard the premise: the figures below were read off a 10-model, 5-wide unit.
        Assert.Equal((10, 5), (greatswords.ModelCount, greatswords.Frontage));
        Assert.Equal((10, 5), (hammerers.ModelCount, hammerers.Frontage));
        return (greatswords, hammerers);
    }

    [Fact]
    public async Task Greatswords_OnTheAttackerSide_MatchWhatThePanelShowed()
    {
        var (greatswords, hammerers) = await Matchup();

        var stats = Calculator.Compute(greatswords, hammerers, ChargeState.Neither, isAttackerSide: true);

        Assert.Equal(5, stats.BaseContact);
        Assert.Equal(10, stats.TotalAttacks);
        Assert.Equal(5, stats.EffectiveStrength);
        Assert.Equal(2, stats.EffectiveInitiative);
        Assert.Equal(4, stats.ToHitNeeded);
        Assert.Equal("3+", stats.ToWoundNeeded.ToString());
        Assert.True(stats.ArmourSave > 6, $"expected no armour save, got {stats.ArmourSave}+");
        Assert.Equal(7, stats.WardSave);
        Assert.Equal(0, stats.ImpactHits);
        Assert.False(stats.IsCharging);
        Assert.Equal(2, stats.StrikePriority);
    }

    [Fact]
    public async Task Hammerers_OnTheDefenderSide_MatchWhatThePanelShowed()
    {
        var (greatswords, hammerers) = await Matchup();

        var stats = Calculator.Compute(hammerers, greatswords, ChargeState.Neither, isAttackerSide: false);

        Assert.Equal(5, stats.BaseContact);
        Assert.Equal(15, stats.TotalAttacks);
        Assert.Equal(6, stats.EffectiveStrength);
        Assert.Equal(1, stats.EffectiveInitiative);
        Assert.Equal(3, stats.ToHitNeeded);
        Assert.Equal("2+", stats.ToWoundNeeded.ToString());
        Assert.Equal(6, stats.ArmourSave);
        Assert.Equal(7, stats.WardSave);
        Assert.Equal(0, stats.ImpactHits);
        Assert.False(stats.IsCharging);
        Assert.Equal(1, stats.StrikePriority);
    }

    // The engine's rider (non-mount) attack group for one side of a matchup.
    private static CombatResolver.AttackGroup RiderGroup(
        UnitConfiguration attacker, UnitConfiguration defender, ChargeState charge, bool attackerSide)
    {
        var context = new CombatContext { Charge = charge, CombatRound = 1 };
        return new CombatResolver(TestHelpers.Repo).PrepareAttackGroups(attacker, defender, context)
            .Single(g => g.IsAttackerSide == attackerSide && !g.IsMountAttack);
    }

    [Theory]
    [InlineData(FactionKey.HighElves, ChargeState.Neither)]
    [InlineData(FactionKey.HighElves, ChargeState.AttackerCharged)]
    [InlineData(FactionKey.DarkElves, ChargeState.Neither)]
    [InlineData(FactionKey.DarkElves, ChargeState.AttackerCharged)]
    [InlineData(FactionKey.TheEmpire, ChargeState.Neither)]
    [InlineData(FactionKey.TheEmpire, ChargeState.AttackerCharged)]
    public async Task InitiativeAndStrikePriority_AgreeWithTheEnginesRiderGroups(FactionKey faction, ChargeState charge)
    {
        var (_, hammerers) = await Matchup();
        var units = await TestHelpers.Repo.GetUnitsAsync(faction);
        var mismatches = new List<string>();

        void Check(UnitConfiguration attacker, UnitConfiguration defender, bool attackerSide)
        {
            var (unit, opponent) = attackerSide ? (attacker, defender) : (defender, attacker);
            var stats = Calculator.Compute(unit, opponent, charge, attackerSide);
            var group = RiderGroup(attacker, defender, charge, attackerSide);
            if ((stats.EffectiveInitiative, stats.StrikePriority) != (group.Initiative, group.Priority))
                mismatches.Add($"{attacker.ResolvedUnit.Name} vs {defender.ResolvedUnit.Name}, "
                    + $"{(attackerSide ? "attacker" : "defender")} side: calculator I{stats.EffectiveInitiative} "
                    + $"P{stats.StrikePriority}, engine I{group.Initiative} P{group.Priority}");
        }

        // Every unit of the faction attacks the fixed opponent, then defends against it.
        foreach (var unit in units)
        {
            var picked = new UnitConfiguration(unit);
            foreach (var attackerSide in new[] { true, false })
            {
                Check(picked, hammerers, attackerSide);
                Check(hammerers, picked, attackerSide);
            }
        }

        Assert.NotEmpty(units);
        Assert.Empty(mismatches);
    }

    [Fact]
    public async Task ASwordmaster_StrikesAtItsProfileInitiativeWhateverItsWeapon()
    {
        var swordmasters = await Picked(FactionKey.HighElves, "Swordmasters of Hoeth");
        var (greatswords, _) = await Matchup();
        int baseI = swordmasters.PrimaryProfile.I!.Value;

        // Guard the premise: a Swordmaster whose selected weapon would lower its Initiative.
        var weaponRules = CombatResolver.ResolveWeapon(swordmasters, TestHelpers.Repo).Rules;
        Assert.True(swordmasters.EffectiveSpecialRules.Swordmaster);
        Assert.True(weaponRules.ApplyToInitiative(baseI, false, false, 1) < baseI,
            $"{swordmasters.SelectedWeapon} should lower Initiative for this test to mean anything");

        var stats = Calculator.Compute(swordmasters, greatswords, ChargeState.Neither, isAttackerSide: true);

        Assert.Equal(baseI, stats.EffectiveInitiative);
        Assert.Equal(RiderGroup(swordmasters, greatswords, ChargeState.Neither, attackerSide: true).Initiative,
            stats.EffectiveInitiative);
    }

    [Fact]
    public async Task OnlyTheChargingSide_ReportsCharging()
    {
        var (greatswords, hammerers) = await Matchup();

        var attacker = Calculator.Compute(greatswords, hammerers, ChargeState.AttackerCharged, isAttackerSide: true);
        var defender = Calculator.Compute(hammerers, greatswords, ChargeState.AttackerCharged, isAttackerSide: false);

        Assert.True(attacker.IsCharging);
        Assert.False(defender.IsCharging);
    }

    [Fact]
    public async Task ChargingCavalry_TakesOneImpactHitPerFrontRankModel()
    {
        // wap-v2 troop-types.json gives Cavalry one inherent Impact Hit per model.
        var knights = await Picked(FactionKey.TheEmpire, "Imperial Knights");
        var (greatswords, _) = await Matchup();
        Assert.Equal(BaseTroopType.Cavalry, knights.TroopType.BaseType);
        Assert.Equal(1, knights.TroopType.InherentImpactHits);

        var charging = Calculator.Compute(knights, greatswords, ChargeState.AttackerCharged, isAttackerSide: true);
        var standing = Calculator.Compute(knights, greatswords, ChargeState.Neither, isAttackerSide: true);

        Assert.True(knights.ModelsInFrontRank > 0);
        Assert.Equal(knights.ModelsInFrontRank, charging.ImpactHits);
        Assert.Equal(0, standing.ImpactHits);
    }
}
