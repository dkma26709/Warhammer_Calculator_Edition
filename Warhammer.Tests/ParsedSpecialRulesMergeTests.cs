using Warhammer.Models.Domain;

namespace Warhammer.Tests;

public class ParsedSpecialRulesMergeTests
{
    [Fact]
    public void MergeBooleans_OrsCorrectly()
    {
        var a = new ParsedSpecialRules { KillingBlow = true, FlamingAttacks = false };
        var b = new ParsedSpecialRules { KillingBlow = false, FlamingAttacks = true };

        var merged = a.Merge(b);

        Assert.True(merged.KillingBlow);
        Assert.True(merged.FlamingAttacks);
    }

    [Fact]
    public void MergeBooleans_BothFalse_StaysFalse()
    {
        var a = new ParsedSpecialRules { Frenzy = false };
        var b = new ParsedSpecialRules { Frenzy = false };

        Assert.False(a.Merge(b).Frenzy);
    }

    [Fact]
    public void MergeWardSave_TakesBest()
    {
        var a = new ParsedSpecialRules { WardSave = 5 };
        var b = new ParsedSpecialRules { WardSave = 4 };

        Assert.Equal(4, a.Merge(b).WardSave);
    }

    [Fact]
    public void MergeWardSave_OneNull_TakesNonNull()
    {
        var a = new ParsedSpecialRules { WardSave = 5 };
        var b = new ParsedSpecialRules { WardSave = null };

        Assert.Equal(5, a.Merge(b).WardSave);
        Assert.Equal(5, b.Merge(a).WardSave);
    }

    [Fact]
    public void MergeRegeneration_TakesBest()
    {
        var a = new ParsedSpecialRules { Regeneration = 6 };
        var b = new ParsedSpecialRules { Regeneration = 4 };

        Assert.Equal(4, a.Merge(b).Regeneration);
    }

    [Fact]
    public void MergeArmourPiercing_TakesHighest()
    {
        var a = new ParsedSpecialRules { ArmourPiercing = 1 };
        var b = new ParsedSpecialRules { ArmourPiercing = 3 };

        Assert.Equal(3, a.Merge(b).ArmourPiercing);
    }

    [Fact]
    public void MergeArmourPiercing_OneNull_TakesNonNull()
    {
        var a = new ParsedSpecialRules { ArmourPiercing = null };
        var b = new ParsedSpecialRules { ArmourPiercing = 2 };

        Assert.Equal(2, a.Merge(b).ArmourPiercing);
    }

    [Fact]
    public void MergeMightyBlow_TakesHighest()
    {
        var a = new ParsedSpecialRules { MightyBlow = 1 };
        var b = new ParsedSpecialRules { MightyBlow = 2 };

        Assert.Equal(2, a.Merge(b).MightyBlow);
    }

    [Fact]
    public void MergeStrings_PrefersNonNull()
    {
        var a = new ParsedSpecialRules { HatredTarget = null };
        var b = new ParsedSpecialRules { HatredTarget = "Orcs" };

        Assert.Equal("Orcs", a.Merge(b).HatredTarget);
    }

    [Fact]
    public void MergeStrings_ThisWinsTies()
    {
        var a = new ParsedSpecialRules { HatredTarget = "Elves" };
        var b = new ParsedSpecialRules { HatredTarget = "Orcs" };

        Assert.Equal("Elves", a.Merge(b).HatredTarget);
    }

    [Fact]
    public void MergeAllRuleNames_Concatenated()
    {
        var a = new ParsedSpecialRules { AllRuleNames = ["Killing Blow"] };
        var b = new ParsedSpecialRules { AllRuleNames = ["Flaming Attacks", "Ward Save (4+)"] };

        var merged = a.Merge(b);
        Assert.Equal(3, merged.AllRuleNames.Count);
        Assert.Contains("Killing Blow", merged.AllRuleNames);
        Assert.Contains("Flaming Attacks", merged.AllRuleNames);
        Assert.Contains("Ward Save (4+)", merged.AllRuleNames);
    }

    [Fact]
    public void MergeWithDefault_ReturnsOriginalValues()
    {
        var original = new ParsedSpecialRules
        {
            KillingBlow = true,
            WardSave = 5,
            ArmourPiercing = 2,
            FlamingAttacks = true,
            HatredTarget = "Elves",
        };
        var empty = new ParsedSpecialRules();

        var merged = original.Merge(empty);

        Assert.True(merged.KillingBlow);
        Assert.Equal(5, merged.WardSave);
        Assert.Equal(2, merged.ArmourPiercing);
        Assert.True(merged.FlamingAttacks);
        Assert.Equal("Elves", merged.HatredTarget);
    }

    [Fact]
    public void MergeCompound_AllFieldTypesCorrect()
    {
        var unit = new ParsedSpecialRules
        {
            Hatred = true,
            WardSave = 6,
            ArmourPiercing = 1,
            MightyBlow = 1,
            AllRuleNames = ["Hatred"],
        };
        var item = new ParsedSpecialRules
        {
            KillingBlow = true,
            WardSave = 4,
            ArmourPiercing = 2,
            FlamingAttacks = true,
            AllRuleNames = ["Killing Blow", "Flaming Attacks"],
        };

        var merged = unit.Merge(item);

        // Booleans OR
        Assert.True(merged.Hatred);
        Assert.True(merged.KillingBlow);
        Assert.True(merged.FlamingAttacks);

        // Saves take best (lower)
        Assert.Equal(4, merged.WardSave);

        // Bonuses take best (higher)
        Assert.Equal(2, merged.ArmourPiercing);
        Assert.Equal(1, merged.MightyBlow);

        // Names concatenated
        Assert.Equal(3, merged.AllRuleNames.Count);
    }
}
