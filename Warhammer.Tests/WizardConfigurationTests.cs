using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class WizardConfigurationTests
{
    [Fact]
    public void IsWizardCapable_FalseWhenNoWizardUpgradeOption()
    {
        var unit = TestHelpers.MakeUnit();
        var config = new UnitConfiguration(unit);

        Assert.False(config.IsWizardCapable);
    }

    [Fact]
    public void IsWizardCapable_TrueWhenWizardUpgradeOptionPresent()
    {
        var unit = TestHelpers.MakeUnit(options: new List<UnitOption>
        {
            new() { Type = "wizard_upgrade", Upgrade = "Level 4 Wizard", Points = 35 },
        });
        var config = new UnitConfiguration(unit);

        Assert.True(config.IsWizardCapable);
    }

    [Fact]
    public void WizardLevel_DefaultsToTwo()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Equal(2, config.WizardLevel);
    }

    [Fact]
    public void SelectedLore_DefaultsToNull()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Null(config.SelectedLore);
    }

    [Fact]
    public void SelectedSpells_DefaultsToEmpty()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Empty(config.SelectedSpells);
    }

    [Fact]
    public void SelectedLoreAndSpells_Mutable()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        config.SelectedLore = "Lore of the Wild";
        config.SelectedSpells.Add("Bestial Surge");
        config.SelectedSpells.Add("Wildform");
        config.WizardLevel = 4;

        Assert.Equal("Lore of the Wild", config.SelectedLore);
        Assert.Equal(2, config.SelectedSpells.Count);
        Assert.Contains("Bestial Surge", config.SelectedSpells);
        Assert.Equal(4, config.WizardLevel);
    }
}
