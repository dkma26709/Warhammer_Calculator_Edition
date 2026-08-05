using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// Body armour is one suit per model — light, medium and heavy are alternatives, not
/// cumulative. Shields, bucklers and barding are separate pieces and do add.
/// </summary>
public class ArmourStackingTests
{
    private static UnitOption AddArmour(string name) => new()
    {
        Type = "equipment",
        Action = "add",
        Choices = [new OptionChoice { Name = [name], Points = 1.5, PerModel = true }],
        Raw = $"May take {name}",
    };

    private static UnitConfiguration Configured(string equipment, string upgrade)
    {
        var unit = TestHelpers.MakeUnit(equipment: equipment, options: [AddArmour(upgrade)]);
        var config = new UnitConfiguration(unit);
        config.SelectedUpgrades[0] = 0;
        return config;
    }

    // ─── The reported symptom ───────────────────────────────────────

    [Fact]
    public void MediumArmourUpgradedToHeavy_GivesFourPlus_NotTwoPlus()
    {
        var config = Configured("Hand weapon, medium armour", "heavy armour");

        // Heavy armour alone is 4+. Summing medium (+2) and heavy (+3) would give 2+.
        Assert.Equal(4, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void UpgradingBodyArmour_ReplacesItInEffectiveArmour()
    {
        var config = Configured("Hand weapon, medium armour", "heavy armour");

        Assert.Contains(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.DoesNotContain(ArmourKey.MediumArmour, config.EffectiveArmour);
    }

    [Fact]
    public void DeselectingTheUpgrade_RestoresTheBaseArmour()
    {
        var config = Configured("Hand weapon, medium armour", "heavy armour");
        config.SelectedUpgrades.Remove(0);

        Assert.Contains(ArmourKey.MediumArmour, config.EffectiveArmour);
        Assert.DoesNotContain(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.Equal(5, config.EffectiveArmourSave(3, 0, 0));
    }

    // ─── Best suit wins, regardless of order ───────────────────────

    [Theory]
    [InlineData("Hand weapon, light armour", "heavy armour", 4)]   // +3 not +4
    [InlineData("Hand weapon, medium armour", "heavy armour", 4)]  // +3 not +5
    [InlineData("Hand weapon, light armour", "medium armour", 5)]  // +2 not +3
    [InlineData("Hand weapon, heavy armour", "light armour", 4)]   // no downgrade
    [InlineData("Hand weapon, heavy armour", "medium armour", 4)]
    public void BodyArmour_KeepsTheBestSuitOnly(string equipment, string upgrade, int expectedSave)
    {
        var config = Configured(equipment, upgrade);
        Assert.Equal(expectedSave, config.EffectiveArmourSave(3, 0, 0));
    }

    // ─── Shields still add on top ──────────────────────────────────

    [Fact]
    public void Shield_StacksWithBodyArmour()
    {
        var config = Configured("Hand weapon, heavy armour", "shield");

        // Heavy armour 4+ improved by a shield = 3+.
        Assert.Equal(3, config.EffectiveArmourSave(3, 0, 0));
        Assert.Contains(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
    }

    [Fact]
    public void ShieldAndBarding_BothAddOnTopOfOneSuit()
    {
        var unit = TestHelpers.MakeUnit(equipment: "Hand weapon, medium armour, shield, barding");
        var config = new UnitConfiguration(unit);

        // Medium 5+ (+2), shield (+1), barding (+1) = 3+.
        Assert.Equal(3, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void DuplicateBodyArmourInBaseEquipment_DoesNotStack()
    {
        // Extracted equipment strings are dirty; two suits listed must not compound.
        var unit = TestHelpers.MakeUnit(equipment: "Hand weapon, medium armour, heavy armour");
        var config = new UnitConfiguration(unit);

        Assert.Equal(4, config.EffectiveArmourSave(3, 0, 0));
    }

    // ─── The real matchup that surfaced it ─────────────────────────

    [Fact]
    public async Task DwarfWarriors_WithHeavyArmour_AreNoBetterArmouredThanLongbeards()
    {
        var repo = TestHelpers.CreateRepository();
        var dwarfs = await repo.GetUnitsAsync(FactionKey.Dwarfs);

        var warriors = dwarfs.First(u => u.Name == "Warriors");
        var longbeards = dwarfs.First(u => u.Name == "Longbeards");

        // Warriors are medium armour with a heavy armour upgrade; Longbeards start heavy.
        var warriorConfig = new UnitConfiguration(warriors);
        int heavyOptionIndex = warriors.Options
            .Select((o, idx) => (o, idx))
            .Where(x => x.o.Choices?.Any(c =>
                c.Name.Any(n => n.Contains("heavy armour", StringComparison.OrdinalIgnoreCase))) == true)
            .Select(x => x.idx)
            .DefaultIfEmpty(-1)
            .First();
        Assert.True(heavyOptionIndex >= 0, "Warriors should offer a heavy armour upgrade");
        warriorConfig.SelectedUpgrades[heavyOptionIndex] = 0;

        var longbeardConfig = new UnitConfiguration(longbeards);

        // Both wear heavy armour, so both are 4+ before any strength penalty, and identical
        // once a great weapon's -2 penalty is applied.
        Assert.Equal(4, longbeardConfig.EffectiveArmourSave(3, 0, 0));
        Assert.Equal(4, warriorConfig.EffectiveArmourSave(3, 0, 0));
        Assert.Equal(
            longbeardConfig.EffectiveArmourSave(5, 0, -2),
            warriorConfig.EffectiveArmourSave(5, 0, -2));
    }
}
