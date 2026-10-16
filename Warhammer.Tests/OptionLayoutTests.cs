using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class OptionLayoutTests
{
    private const string ChooseOne = "May choose one of the following:";

    /// <summary>An equipment option whose choices are each one item.</summary>
    private static UnitOption Equipment(string action, params string[] items) =>
        Group(action, [.. items.Select(i => new[] { i })]);

    /// <summary>An equipment option whose choices may bring several items each.</summary>
    private static UnitOption Group(string action, string[][] choices, string? replaces = null, string raw = ChooseOne) =>
        new()
        {
            Type = "equipment",
            Action = action,
            Replaces = replaces,
            Choices = [.. choices.Select(c => new OptionChoice { Name = [.. c], Points = 1 })],
            Raw = raw,
        };

    private static UnitOption FlatUpgrade(string name) => new() { Type = "upgrade", Upgrade = name, Points = 5 };

    // ─── Empire General, as WAR writes him ───────────────────────

    private static List<UnitOption> EmpireGeneral() =>
    [
        Equipment("choose", "Additional hand weapon", "Spear", "Light lance", "Heavy lance", "Polearm", "Great weapon"),
        Equipment("choose", "Pistol", "Longbow", "Crossbow", "Handgun", "Repeater pistol", "Repeater handgun"),
        Equipment("choose", "Light armour", "Medium armour", "Heavy armour"),
        Equipment("add", "shield"),
        FlatUpgrade("Full Plate"),
        new UnitOption
        {
            Type = "mount",
            Choices = [.. new[] { "Warhorse", "Pegasus", "Griffon", "Imperial Griffon" }
                .Select(m => new OptionChoice { Name = [m], Points = 20 })],
        },
        new UnitOption { Type = "magic_items", PointsLimit = 100 },
    ];

    [Fact]
    public void EmpireGeneral_IsArrangedIntoWeaponsArmourAndMount()
    {
        var placed = OptionLayout.Arrange(EmpireGeneral());

        Assert.Equal(
        [
            (0, OptionSection.Weapons, "Close combat weapon"),
            (1, OptionSection.Weapons, "Missile weapon"),
            (2, OptionSection.Armour, "Armour"),
            (3, OptionSection.Armour, "Shield"),
            (4, OptionSection.Armour, "Full Plate"),
            (5, OptionSection.Mount, "Mount"),
        ], placed.Select(p => (p.Index, p.Section, p.Label)));
    }

    [Fact]
    public void Arrange_KeepsTheOriginalOptionAgainstItsIndex()
    {
        var options = EmpireGeneral();
        var placed = OptionLayout.Arrange(options);

        Assert.All(placed, p => Assert.Same(options[p.Index], p.Option));
    }

    [Fact]
    public void Arrange_OrdersBySectionThenBookOrder()
    {
        var placed = OptionLayout.Arrange(
        [
            new UnitOption { Type = "mount", Choices = [new OptionChoice { Name = ["Warhorse"] }, new OptionChoice { Name = ["Pegasus"] }] },
            Equipment("add", "shield"),
            FlatUpgrade("Poisoned Attacks"),
            Equipment("choose", "Spear", "Great weapon"),
        ]);

        Assert.Equal([3, 1, 0, 2], placed.Select(p => p.Index));
    }

    [Fact]
    public void Arrange_NullOrEmpty_IsEmpty()
    {
        Assert.Empty(OptionLayout.Arrange(null));
        Assert.Empty(OptionLayout.Arrange([]));
    }

    // ─── Equipment sections and labels ───────────────────────────

    [Fact]
    public void WeaponsMixedWithAShield_AreEquipment_NotArmour()
    {
        var option = Equipment("choose", "Additional hand weapons", "Shields", "Polearms", "Great weapons");

        Assert.Equal(OptionSection.Equipment, OptionLayout.SectionOf(option));
        Assert.Equal("Equipment", OptionLayout.LabelFor(option));
        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(option));
    }

    [Fact]
    public void PluralCloseCombatWeapons_AreACloseCombatWeaponGroup()
    {
        var option = Equipment("choose", "Additional hand weapons", "Polearms");

        Assert.Equal(OptionSection.Weapons, OptionLayout.SectionOf(option));
        Assert.Equal("Close combat weapon", OptionLayout.LabelFor(option));
        Assert.True(OptionLayout.IsCloseCombatWeaponGroup(option));
    }

    [Fact]
    public void UnknownGear_TakesTheSectionOfItsGroupMates()
    {
        var option = Equipment("choose", "Shortbow", "Javelins", "Sun Gauntlet", "Sunstaff");

        Assert.Equal(OptionSection.Weapons, OptionLayout.SectionOf(option));
        Assert.Equal("Missile weapon", OptionLayout.LabelFor(option));
        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(option));
    }

    [Fact]
    public void AllUnknownGear_IsEquipment()
    {
        Assert.Equal(OptionSection.Equipment,
            OptionLayout.SectionOf(Equipment("choose", "Sun Gauntlet", "Sunstaff")));
    }

    [Fact]
    public void BothKindsOfWeapon_AreLabelledWeapon()
    {
        var option = Equipment("choose", "Spear", "Shortbow");

        Assert.Equal(OptionSection.Weapons, OptionLayout.SectionOf(option));
        Assert.Equal("Weapon", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void ArmourTheTablesDoNotName_IsStillArmour()
    {
        var option = Group("choose", [["Gut-plates"], ["Light armour"], ["Light armour", "gut plates"]]);

        Assert.Equal(OptionSection.Armour, OptionLayout.SectionOf(option));
        Assert.Equal("Armour", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void ReplaceGroup_IsLabelledByWhatItReplaces()
    {
        var option = Group("replace", [["Shortbows"], ["Javelins"]], replaces: "light lances");

        Assert.Equal(OptionSection.Weapons, OptionLayout.SectionOf(option));
        Assert.Equal("Replace light lances with", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void SingleReplaceChoice_SaysWhatItReplaces()
    {
        var option = Group("replace", [["slings"]], replaces: "javelins");

        Assert.Equal("Slings (replaces javelins)", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void SingleReplaceChoice_PrefersTheChoicesOwnReplaces()
    {
        var option = new UnitOption
        {
            Type = "equipment",
            Action = "replace",
            Replaces = "spear",
            Choices = [new OptionChoice { Name = ["pike"], Replaces = "hand weapon" }],
        };

        Assert.Equal("Pike (replaces hand weapon)", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void ChooseAtLeastOne_SaysSo()
    {
        var option = Group("choose", [["Shields"], ["Great weapons"]],
            raw: "Must choose at least one of the following:");

        Assert.Equal("Equipment (at least one)", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void ChooseAny_SaysSo()
    {
        var option = Group("choose", [["Additional hand weapons"], ["Great weapons"]],
            raw: "May choose any of the following:");

        Assert.Equal("Close combat weapon (any)", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void EquipmentWithoutChoices_IsNotShown()
    {
        Assert.Null(OptionLayout.SectionOf(new UnitOption { Type = "equipment", Action = "choose" }));
        Assert.Null(OptionLayout.SectionOf(new UnitOption { Type = "equipment", Action = "choose", Choices = [] }));
    }

    // ─── Upgrades and the rest ───────────────────────────────────

    [Fact]
    public void UpgradeWithSubOptions_IsAnUpgradeHeadedUpgrade()
    {
        var option = new UnitOption
        {
            Type = "upgrade",
            Upgrade = "May choose one of the following",
            SubOptions = [FlatUpgrade("Ambushers"), FlatUpgrade("Scouts")],
        };

        Assert.Equal(OptionSection.Upgrades, OptionLayout.SectionOf(option));
        Assert.Equal("Upgrade", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void UpgradeWithSubOptions_KeepsARealName()
    {
        var option = new UnitOption
        {
            Type = "upgrade",
            Upgrade = "Veteran",
            SubOptions = [FlatUpgrade("Ambushers")],
        };

        Assert.Equal("Veteran", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void FlatUpgrade_NamingAWeapon_SitsWithWeapons()
    {
        var option = FlatUpgrade("Blowpipe");

        Assert.Equal(OptionSection.Weapons, OptionLayout.SectionOf(option));
        Assert.Equal("Blowpipe", OptionLayout.LabelFor(option));
    }

    [Fact]
    public void FlatUpgrade_NamingArmour_SitsWithArmour()
    {
        Assert.Equal(OptionSection.Armour, OptionLayout.SectionOf(FlatUpgrade("gut-plate")));
    }

    [Fact]
    public void FlatUpgrade_OtherwiseIsAnUpgrade()
    {
        Assert.Equal(OptionSection.Upgrades, OptionLayout.SectionOf(FlatUpgrade("Poisoned Attacks")));
    }

    [Theory]
    [InlineData("wizard_upgrade")]
    [InlineData("blessed_spawning")]
    [InlineData("faction_upgrade")]
    public void FactionAndWizardUpgrades_AreUpgrades(string type)
    {
        var option = new UnitOption { Type = type, Upgrade = "Level 2 Wizard" };

        Assert.Equal(OptionSection.Upgrades, OptionLayout.SectionOf(option));
        Assert.Equal("Level 2 Wizard", OptionLayout.LabelFor(option));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("magic_items")]
    [InlineData("battle_standard")]
    [InlineData("magic_standard")]
    [InlineData("not_a_type")]
    public void PanelsOfTheirOwn_AreNotShown(string type)
    {
        Assert.Null(OptionLayout.SectionOf(new UnitOption { Type = type }));
    }

    // ─── Gear kinds, choice names, close combat groups ───────────

    [Theory]
    [InlineData("Great weapons", GearKind.CloseCombatWeapon)]
    [InlineData("Light lances", GearKind.CloseCombatWeapon)]
    [InlineData("Handgun", GearKind.MissileWeapon)]
    [InlineData("shield", GearKind.Armour)]
    [InlineData("Gut-plates", GearKind.Armour)]
    [InlineData("Fur cloaks", GearKind.Armour)]
    [InlineData("Natural armour (6+)", GearKind.Armour)]
    [InlineData("Full Plate", GearKind.Armour)]
    [InlineData("Shieldbearers", GearKind.Unknown)]
    [InlineData("Sunstaff", GearKind.Unknown)]
    public void KindOf_ClassifiesAnItemName(string name, GearKind expected)
    {
        Assert.Equal(expected, OptionLayout.KindOf(name));
    }

    [Fact]
    public void ChoiceName_CapitalisesTheFirstLetter()
    {
        Assert.Equal("Javelins", OptionLayout.ChoiceName(new OptionChoice { Name = ["javelins"] }));
        Assert.Equal("Light lances & shields",
            OptionLayout.ChoiceName(new OptionChoice { Name = ["light lances", "shields"] }));
        Assert.Equal("", OptionLayout.ChoiceName(new OptionChoice { Name = [] }));
    }

    [Fact]
    public void ChoiceName_DropsTheCostWrittenIntoIt()
    {
        Assert.Equal("Brace of Ogre Pistols",
            OptionLayout.ChoiceName(new OptionChoice { Name = ["brace of Ogre Pistols 6 points"] }));
    }

    [Fact]
    public void ChoicePoints_PrefersItsOwnThenItsNameThenALoneChoicesBookLine()
    {
        var pistols = new OptionChoice { Name = ["brace of Ogre Pistols 6 points"], Points = null };
        Assert.Equal(6, OptionLayout.ChoicePoints(pistols, new UnitOption { Type = "equipment", Choices = [pistols] }));

        var priced = new OptionChoice { Name = ["repeater handgun"], Points = 5 };
        Assert.Equal(5, OptionLayout.ChoicePoints(priced, new UnitOption { Type = "equipment", Choices = [priced], Points = 9 }));

        var pistol = new OptionChoice { Name = ["pistol"], Points = null };
        var lone = new UnitOption { Type = "equipment", Choices = [pistol], Raw = "May take a pistol +3 points" };
        Assert.Equal(3, OptionLayout.ChoicePoints(pistol, lone));
    }

    /// <summary>The Ogres' Look-out Gnoblar, as extracted: its cost left in its name.</summary>
    private static readonly UnitOption Gnoblar = new() { Type = "upgrade", Upgrade = "Look-out Gnoblar 5 points" };

    private static readonly UnitOption BloodVulture = new() { Type = "upgrade", Upgrade = "Blood Vulture", Points = 4 };

    [Fact]
    public void UpgradeName_DropsTheCostWrittenIntoIt()
    {
        Assert.Equal("Look-out Gnoblar", OptionLayout.UpgradeName(Gnoblar));
        Assert.Equal("Blood Vulture", OptionLayout.UpgradeName(BloodVulture));
    }

    [Fact]
    public void UpgradePoints_FallsBackToTheCostWrittenIntoIt()
    {
        Assert.Equal(5, OptionLayout.UpgradePoints(Gnoblar));
        Assert.Equal(4, OptionLayout.UpgradePoints(BloodVulture));
    }

    [Fact]
    public void AChoiceBringingAShield_IsNotAPureCloseCombatGroup()
    {
        var option = Group("choose", [["Spears"], ["Light lances", "shields"]]);

        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(option));
    }

    [Fact]
    public void OnlyEquipmentIsACloseCombatWeaponGroup()
    {
        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(new UnitOption { Type = "command" }));
        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(new UnitOption { Type = "battle_standard" }));
        Assert.False(OptionLayout.IsCloseCombatWeaponGroup(new UnitOption { Type = "equipment", Choices = [] }));
    }
}
