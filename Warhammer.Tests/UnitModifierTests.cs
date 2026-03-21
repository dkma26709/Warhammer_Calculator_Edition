using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class UnitModifierTests
{
    private readonly AttackResolver _resolver;

    public UnitModifierTests()
    {
        _resolver = new AttackResolver(TestHelpers.Repo);
    }

    // ── Empty modifier baseline ──────────────────────────────────

    [Fact]
    public void EmptyModifier_NoEffectOnStrength()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(s: 4));
        var weapon = unit.GetSelectedResolvedWeapon();
        int baseline = unit.EffectiveStrength(weapon, false, 1);

        unit.Modifiers.Add(new UnitModifier());
        int withEmpty = unit.EffectiveStrength(weapon, false, 1);

        Assert.Equal(baseline, withEmpty);
    }

    [Fact]
    public void EmptyModifier_NoEffectOnToughness()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(t: 4));
        int baseline = unit.BestT;

        unit.Modifiers.Add(new UnitModifier());
        Assert.Equal(baseline, unit.BestT);
    }

    [Fact]
    public void EmptyModifier_NoEffectOnAttacks()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(a: 2));
        int baseline = unit.TotalAttacks(false, 1);

        unit.Modifiers.Add(new UnitModifier());
        Assert.Equal(baseline, unit.TotalAttacks(false, 1));
    }

    [Fact]
    public void EmptyModifier_NoEffectOnWeaponSkill()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(ws: 5));
        int baseline = unit.BestWS;

        unit.Modifiers.Add(new UnitModifier());
        Assert.Equal(baseline, unit.BestWS);
    }

    [Fact]
    public void EmptyModifier_EffectiveSpecialRulesMatchesBase()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(specialRules: "Killing Blow, Ward Save (5+)"));
        var baseRules = unit.SpecialRules;
        var effectiveRules = unit.EffectiveSpecialRules;

        Assert.Equal(baseRules.KillingBlow, effectiveRules.KillingBlow);
        Assert.Equal(baseRules.WardSave, effectiveRules.WardSave);
    }

    // ── Stat bonuses ─────────────────────────────────────────────

    [Fact]
    public void StrengthBonus_IncreasesEffectiveStrength()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(s: 3));
        unit.Modifiers.Add(new UnitModifier { StrengthBonus = 2 });

        var weapon = unit.GetSelectedResolvedWeapon();
        int effectiveS = unit.EffectiveStrength(weapon, false, 1);

        Assert.Equal(5, effectiveS);
    }

    [Fact]
    public void ToughnessBonus_IncreasesBestT()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(t: 3));
        unit.Modifiers.Add(new UnitModifier { ToughnessBonus = 1 });

        Assert.Equal(4, unit.BestT);
    }

    [Fact]
    public void AttacksBonus_IncreasesTotalAttacks()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(a: 1));
        unit.ModelCount = 1;
        unit.Frontage = 1;
        int baseline = unit.TotalAttacks(false, 1);

        unit.Modifiers.Add(new UnitModifier { AttacksBonus = 2 });
        int modified = unit.TotalAttacks(false, 1);

        Assert.Equal(baseline + 2, modified);
    }

    [Fact]
    public void WeaponSkillBonus_IncreasesBestWS()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(ws: 4));
        unit.Modifiers.Add(new UnitModifier { WeaponSkillBonus = 1 });

        Assert.Equal(5, unit.BestWS);
    }

    [Fact]
    public void ArmourSaveBonus_ImprovesArmourSave()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(equipment: "Hand weapon, Heavy armour, Shield"));
        int baseline = unit.ComputeBaseArmourModifier();

        unit.Modifiers.Add(new UnitModifier { ArmourSaveBonus = 1 });
        int modified = unit.ComputeBaseArmourModifier();

        Assert.Equal(baseline + 1, modified);
    }

    // ── Multiple modifiers stack ─────────────────────────────────

    [Fact]
    public void MultipleModifiers_StackAdditively()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(s: 3));
        unit.Modifiers.Add(new UnitModifier { StrengthBonus = 1 });
        unit.Modifiers.Add(new UnitModifier { StrengthBonus = 1 });

        var weapon = unit.GetSelectedResolvedWeapon();
        Assert.Equal(5, unit.EffectiveStrength(weapon, false, 1));
    }

    // ── Rule grants via modifiers ────────────────────────────────

    [Fact]
    public void KillingBlowFromModifier_EnablesKillingBlow()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.False(unit.EffectiveSpecialRules.KillingBlow);

        unit.Modifiers.Add(new UnitModifier
        {
            RuleOverrides = new ParsedSpecialRules { KillingBlow = true },
        });

        Assert.True(unit.EffectiveSpecialRules.KillingBlow);
    }

    [Fact]
    public void WardSaveFromModifier_TakesBestWithExisting()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(specialRules: "Ward Save (5+)"));
        Assert.Equal(5, unit.EffectiveSpecialRules.WardSave);

        // Better ward (4+) from modifier should win
        unit.Modifiers.Add(new UnitModifier
        {
            RuleOverrides = new ParsedSpecialRules { WardSave = 4 },
        });
        Assert.Equal(4, unit.EffectiveSpecialRules.WardSave);
    }

    [Fact]
    public void WardSaveFromModifier_ExistingBetterThanModifier_KeepsExisting()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(specialRules: "Ward Save (4+)"));

        // Worse ward (6+) from modifier should not override
        unit.Modifiers.Add(new UnitModifier
        {
            RuleOverrides = new ParsedSpecialRules { WardSave = 6 },
        });
        Assert.Equal(4, unit.EffectiveSpecialRules.WardSave);
    }

    [Fact]
    public void FrenzyFromModifier_IncreasesAttacks()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(a: 1));
        unit.ModelCount = 1;
        unit.Frontage = 1;
        int baseline = unit.TotalAttacks(false, 1);

        unit.Modifiers.Add(new UnitModifier
        {
            RuleOverrides = new ParsedSpecialRules { Frenzy = true },
        });
        int modified = unit.TotalAttacks(false, 1);

        Assert.Equal(baseline + 1, modified);
    }

    // ── Magic item selection ─────────────────────────────────────

    [Fact]
    public void SelectedMagicItem_ModifierApplied()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(s: 4));
        unit.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Name = "Sword of Might",
            Category = "magic_weapon",
            PointsCost = 20,
            Modifier = new UnitModifier { StrengthBonus = 1 },
        });

        var weapon = unit.GetSelectedResolvedWeapon();
        Assert.Equal(5, unit.EffectiveStrength(weapon, false, 1));
    }

    [Fact]
    public void SelectedMagicItem_PointsCostIncludedInTotal()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(points: 50));
        unit.ModelCount = 1;
        double baseline = unit.TotalPointsCost;

        unit.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Name = "Enchanted Shield",
            Category = "magic_armour",
            PointsCost = 15,
            Modifier = new UnitModifier { ArmourSaveBonus = 1 },
        });

        Assert.Equal(baseline + 15, unit.TotalPointsCost);
    }

    [Fact]
    public void MagicItemAndManualModifier_BothApply()
    {
        var unit = new UnitConfiguration(TestHelpers.MakeUnit(s: 3));
        unit.SelectedMagicItems.Add(new SelectedMagicItem
        {
            Name = "Sword of Might",
            Category = "magic_weapon",
            PointsCost = 20,
            Modifier = new UnitModifier { StrengthBonus = 1 },
        });
        unit.Modifiers.Add(new UnitModifier { StrengthBonus = 1, Source = "Spell" });

        var weapon = unit.GetSelectedResolvedWeapon();
        Assert.Equal(5, unit.EffectiveStrength(weapon, false, 1));
    }

    // ── Aggregate method ─────────────────────────────────────────

    [Fact]
    public void Aggregate_SumsStatBonuses()
    {
        var modifiers = new[]
        {
            new UnitModifier { StrengthBonus = 1, AttacksBonus = 2 },
            new UnitModifier { StrengthBonus = 2, ToughnessBonus = 1 },
        };

        var aggregate = UnitModifier.Aggregate(modifiers);

        Assert.Equal(3, aggregate.StrengthBonus);
        Assert.Equal(2, aggregate.AttacksBonus);
        Assert.Equal(1, aggregate.ToughnessBonus);
    }

    [Fact]
    public void Aggregate_FixedThresholds_TakeBest()
    {
        var modifiers = new[]
        {
            new UnitModifier { FixedToWound = 4 },
            new UnitModifier { FixedToWound = 2 },
        };

        var aggregate = UnitModifier.Aggregate(modifiers);
        Assert.Equal(2, aggregate.FixedToWound);
    }

    [Fact]
    public void Aggregate_BoolRerolls_Or()
    {
        var modifiers = new[]
        {
            new UnitModifier { RerollFailedToHit = false },
            new UnitModifier { RerollFailedToHit = true },
        };

        var aggregate = UnitModifier.Aggregate(modifiers);
        Assert.True(aggregate.RerollFailedToHit);
    }

    [Fact]
    public void Aggregate_EmptyList_ReturnsNeutral()
    {
        var aggregate = UnitModifier.Aggregate([]);

        Assert.Null(aggregate.StrengthBonus);
        Assert.Null(aggregate.FixedToWound);
        Assert.False(aggregate.RerollFailedToHit);
        Assert.Null(aggregate.RuleOverrides);
    }
}
