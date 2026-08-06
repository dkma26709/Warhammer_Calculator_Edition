using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// "This rule is cumulative with other sources of Mighty Blow" — the same sentence appears in
/// 2.32 p63 and 3.11 p64, so this is not a v3 divergence and both systems are covered. The
/// engine took the max of the unit's bonus and its weapon's, which understated first-round
/// Strength on every unit carrying both.
/// </summary>
public class MightyBlowCumulativeTests
{
    /// <summary>An S3 unit carrying the given rule text, with no weapon of its own.</summary>
    private static UnitConfiguration Unit(string? specialRules) =>
        new(TestHelpers.MakeUnit(s: 3, specialRules: specialRules));

    [Theory]
    [InlineData(0, 0, 3)]   // no Mighty Blow anywhere
    [InlineData(1, 0, 4)]   // unit only
    [InlineData(0, 2, 5)]   // weapon only
    [InlineData(1, 2, 6)]   // both add: Boar Boy (1) with a heavy lance (2) reaches S6
    [InlineData(2, 2, 7)]   // flail on a unit that already had (2)
    public void UnitAndWeaponMightyBlowAdd(int unitMB, int weaponMB, int expectedStrength)
    {
        var unit = Unit(unitMB == 0 ? null : $"Mighty Blow ({unitMB})");

        int s = unit.EffectiveStrength(
            weapon: null, isCharging: true, combatRound: 1, weaponMightyBlow: weaponMB);

        Assert.Equal(expectedStrength, s);
    }

    /// <summary>Still first round only — that gating was already right and must stay.</summary>
    [Fact]
    public void CumulativeMightyBlowStillOnlyAppliesInTheFirstRound()
    {
        var unit = Unit("Mighty Blow (1)");

        Assert.Equal(6, unit.EffectiveStrength(null, true, 1, 2));
        Assert.Equal(3, unit.EffectiveStrength(null, true, 2, 2));
    }

    /// <summary>
    /// Thunderous Charge "has the Mighty Blow (2) special rule in turns when it charges", so it
    /// is another source and adds to the unit's own rather than masking it. wap-v2 only.
    /// </summary>
    [Fact]
    public void ThunderousChargeAddsToTheUnitsOwnMightyBlow()
    {
        var unit = Unit("Mighty Blow (1), Thunderous Charge");

        Assert.Equal(6, unit.EffectiveStrength(null, isCharging: true, combatRound: 1, 0));
        Assert.Equal(4, unit.EffectiveStrength(null, isCharging: false, combatRound: 1, 0));
    }

    [Fact]
    public void ThunderousChargeAloneStillGivesTwo()
    {
        var unit = Unit("Thunderous Charge");

        Assert.Equal(5, unit.EffectiveStrength(null, isCharging: true, combatRound: 1, 0));
    }

    // ─── Merge combines separate sources, one list does not ────────

    /// <summary>
    /// Merge joins genuinely distinct sources — base rules plus a magic item or upgrade — so it
    /// adds.
    /// </summary>
    [Fact]
    public void MergingTwoSourcesAddsTheirMightyBlow()
    {
        var merged = new ParsedSpecialRules { MightyBlow = 1 }
            .Merge(new ParsedSpecialRules { MightyBlow = 2 });

        Assert.Equal(3, merged.MightyBlow);
    }

    [Fact]
    public void MergingWhenOnlyOneSideHasMightyBlowKeepsIt()
    {
        Assert.Equal(2, new ParsedSpecialRules { MightyBlow = 2 }
            .Merge(new ParsedSpecialRules()).MightyBlow);
        Assert.Equal(2, new ParsedSpecialRules()
            .Merge(new ParsedSpecialRules { MightyBlow = 2 }).MightyBlow);
        Assert.Null(new ParsedSpecialRules().Merge(new ParsedSpecialRules()).MightyBlow);
    }

    /// <summary>
    /// One rule list is NOT additive. Every unit that names Mighty Blow twice is a rider/mount
    /// split — "Mighty Blow (1), Mighty Blow (2) (War Boars only)" — where the two numbers belong
    /// to two different models, so adding them would arm the rider with the mount's bonus.
    /// </summary>
    [Fact]
    public void TwoEntriesInOneListDoNotAdd()
    {
        var rules = ParsedSpecialRules.Parse("Mighty Blow (1), Mighty Blow (2)");

        Assert.Equal(2, rules.MightyBlow);
    }

    /// <summary>
    /// Armour Piercing says "cumulative with other sources" too (2.32 p58) but is deliberately
    /// left on max() in this change — it is tracked separately, and this test pins the current
    /// behaviour so the difference is a decision rather than a drift.
    /// </summary>
    [Fact]
    public void ArmourPiercingStillTakesTheBestOfItsSources()
    {
        var merged = new ParsedSpecialRules { ArmourPiercing = 1 }
            .Merge(new ParsedSpecialRules { ArmourPiercing = 2 });

        Assert.Equal(2, merged.ArmourPiercing);
    }
}
