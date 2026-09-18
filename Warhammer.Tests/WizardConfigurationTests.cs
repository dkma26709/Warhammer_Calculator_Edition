using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class WizardConfigurationTests
{
    [Fact]
    public void IsWizardCapable_FalseWhenNoWizardLevelOption()
    {
        var unit = TestHelpers.MakeUnit();
        var config = new UnitConfiguration(unit);

        Assert.False(config.IsWizardCapable);
    }

    [Fact]
    public void IsWizardCapable_TrueWhenWizardLevelOptionPresent()
    {
        var unit = TestHelpers.MakeUnit(options: [TestHelpers.WizardLevel(35)]);
        var config = new UnitConfiguration(unit);

        Assert.True(config.IsWizardCapable);
    }

    [Fact]
    public void IsWizardCapable_TrueWhenTheBookGivesAWizardParagraph()
    {
        var unit = TestHelpers.MakeUnit();
        unit = unit with { RawEntry = unit.RawEntry with { Magic = "A Battle Wizard is a Level 1 Wizard." } };
        var config = new UnitConfiguration(unit);

        Assert.True(config.IsWizardCapable);
    }

    [Fact]
    public void AWizardLevelUpgrade_CostsItsPoints()
    {
        var unit = TestHelpers.MakeUnit(points: 65, troopType: "Infantry (Character)", options: [TestHelpers.WizardLevel(35)]);
        var config = new UnitConfiguration(unit);
        Assert.Equal(65, config.TotalPointsCost);

        config.Selections.Toggle("wizard-level", "additional-wizard-level", true);
        Assert.Equal(100, config.TotalPointsCost);
    }

    [Fact]
    public void WizardLevel_DefaultsToOne()
    {
        var config = new UnitConfiguration(TestHelpers.MakeUnit());
        Assert.Equal(1, config.Loadout.WizardLevel);
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

    [Fact]
    public async Task AFactionsLores_AreItsOwnThenTheRulebooks()
    {
        var lores = await TestHelpers.Repo.GetLoresAsync("bretonnia");

        Assert.Equal("THE LORE OF THE LADY", lores[0].Name);
        Assert.Contains(lores, l => l.Name == "THE LORE OF FIRE");
        Assert.All(lores, l => Assert.NotEmpty(l.Spells));
    }
}
