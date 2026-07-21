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
        Assert.Equal(2, config.Loadout.WizardLevel);
    }

    [Fact]
    public void SelectedLore_DefaultsToNull()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Null(config.Loadout.SelectedLore);
    }

    [Fact]
    public void SelectedSpells_DefaultsToEmpty()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Empty(config.Loadout.SelectedSpells);
    }

    [Fact]
    public void SelectedLoreAndSpells_Mutable()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        config.Loadout.SelectedLore = "Lore of the Wild";
        config.Loadout.SelectedSpells.Add("Bestial Surge");
        config.Loadout.SelectedSpells.Add("Wildform");
        config.Loadout.WizardLevel = 4;

        Assert.Equal("Lore of the Wild", config.Loadout.SelectedLore);
        Assert.Equal(2, config.Loadout.SelectedSpells.Count);
        Assert.Contains("Bestial Surge", config.Loadout.SelectedSpells);
        Assert.Equal(4, config.Loadout.WizardLevel);
    }
}
