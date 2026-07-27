using Warhammer.Models.Domain;

namespace Warhammer.Tests;

/// <summary>
/// Shadow Dances and Tree Aspects are expressed as UnitModifiers so their effects
/// flow through the standard modifier pipeline (EffectiveSpecialRules, BestT,
/// TotalAttacks) instead of being special-cased in the combat engine.
/// </summary>
public class FactionAbilityModifierTests
{
    private static UnitConfiguration MakeConfig() =>
        new(TestHelpers.MakeUnit()) { ModelCount = 5, Frontage = 5 };

    // ─── Defaults ────────────────────────────────────────────────

    [Fact]
    public void NoSelection_HasNoModifierEffects()
    {
        var config = MakeConfig();

        Assert.Equal(3, config.BestT);
        Assert.False(config.EffectiveSpecialRules.AlwaysStrikesFirst);
        Assert.False(config.EffectiveSpecialRules.KillingBlow);
        Assert.False(config.EffectiveSpecialRules.Minus1ToBeHit);
        Assert.Null(config.EffectiveSpecialRules.ArmourPiercing);
    }

    // ─── Shadow Dances of Loec ───────────────────────────────────

    [Fact]
    public void WhirlingDeath_GrantsKillingBlowAndAP1()
    {
        var config = MakeConfig();
        config.SelectedShadowDance = ShadowDance.WhirlingDeath;

        Assert.True(config.EffectiveSpecialRules.KillingBlow);
        Assert.Equal(1, config.EffectiveSpecialRules.ArmourPiercing);
    }

    [Fact]
    public void StormOfBlades_AddsOneAttackPerModel()
    {
        var config = MakeConfig();
        int baseline = config.TotalAttacks(isCharging: false, combatRound: 1);

        config.SelectedShadowDance = ShadowDance.StormOfBlades;
        int boosted = config.TotalAttacks(isCharging: false, combatRound: 1);

        // 5 front-rank models gain +1 attack each.
        Assert.Equal(baseline + 5, boosted);
    }

    [Fact]
    public void TheShadowsCoil_GrantsMinus1ToBeHit()
    {
        var config = MakeConfig();
        config.SelectedShadowDance = ShadowDance.TheShadowsCoil;

        Assert.True(config.EffectiveSpecialRules.Minus1ToBeHit);
    }

    [Fact]
    public void WovenMist_GrantsAlwaysStrikesFirst()
    {
        var config = MakeConfig();
        config.SelectedShadowDance = ShadowDance.WovenMist;

        Assert.True(config.EffectiveSpecialRules.AlwaysStrikesFirst);
    }

    // ─── Tree Aspects ────────────────────────────────────────────

    [Fact]
    public void Oak_AddsOneToughness()
    {
        var config = MakeConfig();
        config.SelectedTreeAspect = TreeAspect.Oak;

        Assert.Equal(4, config.BestT);
    }

    [Fact]
    public void Birch_AddsOneAttackPerModel()
    {
        var config = MakeConfig();
        int baseline = config.TotalAttacks(isCharging: false, combatRound: 1);

        config.SelectedTreeAspect = TreeAspect.Birch;
        int boosted = config.TotalAttacks(isCharging: false, combatRound: 1);

        Assert.Equal(baseline + 5, boosted);
    }

    [Fact]
    public void Willow_GrantsMinus1ToBeHit()
    {
        var config = MakeConfig();
        config.SelectedTreeAspect = TreeAspect.Willow;

        Assert.True(config.EffectiveSpecialRules.Minus1ToBeHit);
    }

    // ─── Composition with other modifiers ────────────────────────

    [Fact]
    public void FactionAbility_ComposesWithManualModifiers()
    {
        var config = MakeConfig();
        config.SelectedTreeAspect = TreeAspect.Oak;
        config.Modifiers.Add(new UnitModifier { Label = "Spell", ToughnessBonus = 1 });

        // Base T3 + Oak +1 + spell +1.
        Assert.Equal(5, config.BestT);
    }

    [Fact]
    public void WhirlingDeath_APStacksAsMaxWithBaseRules()
    {
        var unit = TestHelpers.MakeUnit(specialRules: "Armour Piercing (2)");
        var config = new UnitConfiguration(unit) { ModelCount = 5, Frontage = 5 };
        config.SelectedShadowDance = ShadowDance.WhirlingDeath;

        // Merge takes the better AP value, not the sum.
        Assert.Equal(2, config.EffectiveSpecialRules.ArmourPiercing);
    }
}
