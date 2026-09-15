using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class UnitConfigurationTests
{
    private static Unit MakeUnit(
        string name = "Test Unit",
        string troopType = "Infantry",
        int m = 4, int ws = 4, int bs = 3, int s = 3, int t = 3, int w = 1, int i = 3, int a = 1, int ld = 7,
        double? points = 8,
        string? equipment = "Hand weapon",
        string? specialRules = null,
        string? unitSize = "10+",
        string? baseSize = "25x25",
        ProfileEntry? mountProfile = null,
        List<OptionGroup>? options = null)
        => TestHelpers.MakeUnit(name, troopType, m, ws, bs, s, t, w, i, a, ld, points,
            equipment, specialRules, unitSize, baseSize, mountProfile, options);

    [Fact]
    public void TotalAttacks_BasicInfantry_10Models_Frontage5()
    {
        var unit = MakeUnit(a: 1, unitSize: "10+");
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        // 5 front rank + 5 support (1 support rank, 1 attack each)
        Assert.Equal(10, config.TotalAttacks(false, 1));
    }

    [Fact]
    public void TotalAttacks_WithFrenzy_DoublesBaseAttacks()
    {
        var unit = MakeUnit(a: 1, specialRules: "Frenzy");
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        // Frenzy +1A: each model has 2 attacks
        // 5 front * 2 = 10, 5 support * min(2, 1) = 5
        Assert.Equal(15, config.TotalAttacks(false, 1));
    }

    [Fact]
    public void TotalAttacks_NoChampionBonus()
    {
        var unit = MakeUnit(a: 1);
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5, HasChampion = true };
        // Champion does NOT add +1 attack
        // 5 front * 1 = 5, 5 support * 1 = 5
        Assert.Equal(10, config.TotalAttacks(false, 1));
    }

    [Fact]
    public void RankBonus_20Models_Frontage5_Equals2()
    {
        var unit = MakeUnit(unitSize: "20+");
        var config = new UnitConfiguration(unit) { ModelCount = 20, Frontage = 5 };
        // 20/5 = 4 ranks, rank bonus = 4-1 = 3 (capped at 3)
        Assert.Equal(3, config.RankBonus);
    }

    [Fact]
    public void RankBonus_15Models_Frontage5_Equals2()
    {
        var unit = MakeUnit(unitSize: "15+");
        var config = new UnitConfiguration(unit) { ModelCount = 15, Frontage = 5 };
        // 15/5 = 3 ranks, rank bonus = 3-1 = 2
        Assert.Equal(2, config.RankBonus);
    }

    [Fact]
    public void RankBonus_CappedAt3()
    {
        var unit = MakeUnit(unitSize: "30+");
        var config = new UnitConfiguration(unit) { ModelCount = 30, Frontage = 5 };
        // 30/5 = 6 ranks, rank bonus = min(5, 3) = 3
        Assert.Equal(3, config.RankBonus);
    }

    [Fact]
    public void RankBonus_BelowMinWidth_ReturnsZero()
    {
        var unit = MakeUnit(); // Infantry, MinRankWidth = 5
        var config = new UnitConfiguration(unit) { ModelCount = 9, Frontage = 3 };
        // Frontage 3 < Infantry min width 5 → no rank bonus
        Assert.Equal(0, config.RankBonus);
    }

    [Fact]
    public void MountAttacks_Cavalry_FrontRankOnly()
    {
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, BS = 0, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
        var unit = MakeUnit(troopType: "Cavalry", unitSize: "5+", mountProfile: mount);
        var config = new UnitConfiguration(unit) { ModelCount = 5, Frontage = 5 };
        // 5 front * 1A = 5 mount attacks, no support (only 1 rank)
        Assert.Equal(5, config.MountAttacks());
    }

    [Fact]
    public void MountAttacks_Cavalry_MountsDoNotSupport()
    {
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, BS = 0, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
        var unit = MakeUnit(troopType: "Cavalry", unitSize: "10+", mountProfile: mount);
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        // 5 front * 1A = 5; "Mounts are not allowed to make supporting attacks"
        Assert.Equal(5, config.MountAttacks());
    }

    [Fact]
    public void MountAttacks_NonSplitProfile_ReturnsZero()
    {
        var unit = MakeUnit(troopType: "Infantry");
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        Assert.Equal(0, config.MountAttacks());
    }

    [Fact]
    public void ResolveProfiles_SingleProfile_NullMount()
    {
        var unit = MakeUnit();
        var (priced, mount) = UnitConfiguration.ResolveProfiles(unit.RawEntry);
        Assert.Single(priced);
        Assert.Null(mount);
    }

    [Fact]
    public void ResolveProfiles_TwoProfiles_PrimaryHasPoints()
    {
        var p1 = new ProfileEntry { Id = "rider", Name = "Rider", Points = 10 };
        var p2 = new ProfileEntry { Id = "mount", Name = "Mount" };
        var unit = new UnitEntry { Name = "Cavalry", Profiles = [p1, p2], TroopType = "Cavalry" };
        var (priced, mount) = UnitConfiguration.ResolveProfiles(unit);
        Assert.Equal("Rider", Assert.Single(priced).Name);
        Assert.Equal("Mount", mount!.Name);
    }

    [Fact]
    public void ResolveProfiles_TwoPricedProfiles_AreBothVariants()
    {
        var general = new ProfileEntry { Id = "general", Name = "General", Points = 95 };
        var captain = new ProfileEntry { Id = "captain", Name = "Captain", Points = 55 };
        var unit = new UnitEntry { Name = "Commanders", Profiles = [general, captain], TroopType = "Infantry (Character)" };
        var (priced, mount) = UnitConfiguration.ResolveProfiles(unit);
        Assert.Equal(["General", "Captain"], priced.Select(p => p.Name));
        Assert.Null(mount);
    }

    [Fact]
    public void ParseBaseWidthMm_Standard()
    {
        Assert.Equal(25, UnitConfiguration.ParseBaseWidthMm("25x25"));
        Assert.Equal(50, UnitConfiguration.ParseBaseWidthMm("50x50"));
    }

    [Fact]
    public void ParseBaseWidthMm_MultipleOptions_TakesFirst()
    {
        Assert.Equal(50, UnitConfiguration.ParseBaseWidthMm("50x50, 60x60 or 75x75"));
    }

    [Fact]
    public void ParseBaseWidthMm_Null_DefaultsTo25()
    {
        Assert.Equal(25, UnitConfiguration.ParseBaseWidthMm(null));
    }

    [Fact]
    public void ParseMinUnitSize_Standard()
    {
        Assert.Equal(10, UnitConfiguration.ParseMinUnitSize("10+"));
        Assert.Equal(5, UnitConfiguration.ParseMinUnitSize("5+"));
    }

    [Fact]
    public void ParseMinUnitSize_Null_DefaultsTo1()
    {
        Assert.Equal(1, UnitConfiguration.ParseMinUnitSize(null));
    }

    [Fact]
    public void TotalAttacks_WithWeaponExtraAttacks()
    {
        var unit = MakeUnit(a: 1);
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        // +1 attack from weapon (Two Hand Weapons)
        // 5 front * 2 = 10, 5 support * min(2, 1) = 5
        Assert.Equal(15, config.TotalAttacks(false, 1, weaponExtraAttacks: 1));
    }

    [Fact]
    public void TotalAttacks_WithWeaponFiER()
    {
        var unit = MakeUnit(a: 1, unitSize: "20+");
        var config = new UnitConfiguration(unit) { ModelCount = 20, Frontage = 5 };
        // Without FiER: 5 front + 5 support (1 rank) = 10
        Assert.Equal(10, config.TotalAttacks(false, 1));
        // With weapon FiER(1): 5 front + 10 support (2 ranks) = 15
        Assert.Equal(15, config.TotalAttacks(false, 1, weaponFiER: 1));
    }

    [Fact]
    public void EffectiveStrength_BasicWeapon()
    {
        var unit = MakeUnit(s: 3);
        var config = new UnitConfiguration(unit);
        var weapon = new Weapon
        {
            Key = CloseCombatWeaponKey.HandWeapon, Name = "Hand Weapon",
            Strength = WeaponStrength.Parse("As user"), Rules = new ParsedWeaponRules(),
        };
        Assert.Equal(3, config.EffectiveStrength(weapon, false, 1));
    }

    [Fact]
    public void EffectiveStrength_PlusStrength()
    {
        var unit = MakeUnit(s: 4);
        var config = new UnitConfiguration(unit);
        var weapon = new Weapon
        {
            Key = CloseCombatWeaponKey.GreatWeapon, Name = "Great Weapon",
            Strength = WeaponStrength.Parse("+1"), Rules = new ParsedWeaponRules(),
        };
        Assert.Equal(5, config.EffectiveStrength(weapon, false, 2)); // no MB, round 2
    }

    [Fact]
    public void EffectiveStrength_UnitMightyBlow_FirstRound()
    {
        var unit = MakeUnit(s: 3, specialRules: "Mighty Blow (1)");
        var config = new UnitConfiguration(unit);
        var weapon = new Weapon
        {
            Key = CloseCombatWeaponKey.HandWeapon, Name = "Hand Weapon",
            Strength = WeaponStrength.Parse("As user"), Rules = new ParsedWeaponRules(),
        };
        Assert.Equal(4, config.EffectiveStrength(weapon, false, 1)); // round 1: +1 MB
        Assert.Equal(3, config.EffectiveStrength(weapon, false, 2)); // round 2: no MB
    }

    [Fact]
    public void EffectiveStrength_WeaponMightyBlow_IsCumulativeWithTheUnits()
    {
        var unit = MakeUnit(s: 3, specialRules: "Mighty Blow (1)");
        var config = new UnitConfiguration(unit);
        var weapon = new Weapon
        {
            Key = CloseCombatWeaponKey.Flail, Name = "Flail",
            Strength = WeaponStrength.Parse("As user"), Rules = new ParsedWeaponRules(),
        };
        // Unit MB=1 plus the flail's 2 — "cumulative with other sources of Mighty Blow".
        Assert.Equal(6, config.EffectiveStrength(weapon, false, 1, weaponMightyBlow: 2));
    }

    [Fact]
    public void EffectiveArmourSave_NoArmour_Returns7()
    {
        var unit = MakeUnit(equipment: "Hand weapon");
        var config = new UnitConfiguration(unit);
        Assert.Equal(7, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void EffectiveArmourSave_WithArmour()
    {
        var unit = MakeUnit(equipment: "Hand weapon, heavy armour, shield");
        var config = new UnitConfiguration(unit);
        // heavy armour +3, shield +1 → total modifier = 4, save = 7-4 = 3+
        Assert.Equal(3, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void EffectiveArmourSave_StrengthModifier()
    {
        var unit = MakeUnit(equipment: "Hand weapon, heavy armour");
        var config = new UnitConfiguration(unit);
        // base save 4+, strength mod -1 → 5+
        Assert.Equal(5, config.EffectiveArmourSave(4, 0, -1));
    }

    [Fact]
    public void EffectiveArmourSave_WithNaturalArmour()
    {
        var unit = MakeUnit(specialRules: "Natural Armour (5+)");
        var config = new UnitConfiguration(unit);
        // Natural Armour (5+) → modifier = 7-5 = 2, save = 7-2 = 5+
        Assert.Equal(5, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void EffectiveArmourSave_MinimumIs2Plus()
    {
        var unit = MakeUnit(equipment: "Hand weapon, heavy armour, shield", specialRules: "Natural Armour (4+)");
        var config = new UnitConfiguration(unit);
        // heavy armour +3, shield +1, natural armour 7-4=3 → total 7 → save = 7-7 = 0, clamped to 2
        Assert.Equal(2, config.EffectiveArmourSave(3, 0, 0));
    }

    // --- Equipment Upgrade Tests ---

    [Fact]
    public void ShieldUpgrade_AddsToEffectiveArmour()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, light armour",
            options: [TestHelpers.Equipment("shield", 1, perModel: true)]);

        var config = new UnitConfiguration(unit);
        Assert.DoesNotContain(ArmourKey.Shields, config.EffectiveArmour);

        config.Selections.Toggle("shield", "shield", true);
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
        Assert.Contains(ArmourKey.LightArmour, config.EffectiveArmour);
    }

    [Fact]
    public void ShieldUpgrade_ImprovesArmourSave()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, light armour",
            options: [TestHelpers.Equipment("shield", 1, perModel: true)]);

        var config = new UnitConfiguration(unit);

        // Without shield: light armour +1 → save 6+
        Assert.Equal(6, config.EffectiveArmourSave(3, 0, 0));

        // With shield: light armour +1 + shield +1 → save 5+
        config.Selections.Toggle("shield", "shield", true);
        Assert.Equal(5, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void UpgradeCost_PerModel()
    {
        var unit = MakeUnit(options: [TestHelpers.Equipment("shield", 1, perModel: true)]);

        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        Assert.Equal(0, config.UpgradeCost);

        config.Selections.Toggle("shield", "shield", true);
        Assert.Equal(20, config.UpgradeCost);
    }

    [Fact]
    public void UpgradeCost_FlatCost()
    {
        var unit = MakeUnit(options: [TestHelpers.Equipment("heavy armour", 9)]);

        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.Selections.Toggle("heavy-armour", "heavy-armour", true);
        Assert.Equal(9, config.UpgradeCost);
    }

    [Fact]
    public void UpgradeCost_IncludedInTotalPoints()
    {
        var unit = MakeUnit(points: 8, options: [TestHelpers.Equipment("shield", 1, perModel: true)]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        Assert.Equal(80, config.TotalPointsCost); // 10 * 8 = 80

        config.Selections.Toggle("shield", "shield", true);
        Assert.Equal(90, config.TotalPointsCost); // 80 + 10 * 1 = 90
    }

    // ─── "Choose any" groups (multi-select) ──────────────────────

    [Fact]
    public void ChooseAny_CostsEverySelectedChoice()
    {
        // The Stegadon's book line: any of Stampede (10), Horns (20), Ancient (30).
        var unit = MakeUnit(points: 215, options:
        [
            TestHelpers.Equipment("upgrades", SelectMode.Any,
                TestHelpers.Choice("Unstoppable Stampede", 10),
                TestHelpers.Choice("Sharpened Horns", 20),
                TestHelpers.Choice("Ancient", 30)),
        ]);

        var config = new UnitConfiguration(unit) { ModelCount = 1 };
        config.Selections.Toggle("upgrades", "unstoppable-stampede", true);
        config.Selections.Toggle("upgrades", "sharpened-horns", true);
        config.Selections.Toggle("upgrades", "ancient", true);

        Assert.Equal(60, config.UpgradeCost);
        Assert.Equal(275, config.TotalPointsCost);
    }

    [Fact]
    public void ChooseAny_EquipmentChoices_AllTakeEffect()
    {
        // Dwarf Warriors: "Must choose at least one of the following: shield, great weapon".
        var unit = MakeUnit(equipment: "Hand weapon", options:
        [
            TestHelpers.Equipment("gear", SelectMode.Any,
                TestHelpers.Choice("shield", 1, perModel: true),
                TestHelpers.Choice("great weapon", 2, perModel: true)),
        ]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Selections.Toggle("gear", "shield", true);
        config.Selections.Toggle("gear", "great-weapon", true);

        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
        Assert.Contains(CloseCombatWeaponKey.GreatWeapon, config.EffectiveWeapons);
        Assert.Equal(30, config.UpgradeCost); // 10 × (1 + 2)
    }

    [Fact]
    public void SelectOne_ReplacesThePreviousChoiceInTheGroup()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, medium armour",
            options:
            [
                TestHelpers.Equipment("armour", SelectMode.One,
                    TestHelpers.Choice("heavy armour", 9),
                    TestHelpers.Choice("shield", 3)),
            ]);

        var config = new UnitConfiguration(unit);

        // Select heavy armour
        config.Selections.SelectOne("armour", "heavy-armour");
        Assert.Contains(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.DoesNotContain(ArmourKey.Shields, config.EffectiveArmour);

        // Switch to shield
        config.Selections.SelectOne("armour", "shield");
        Assert.DoesNotContain(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
    }

    [Fact]
    public void ReplaceChoice_RemovesBaseEquipment()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, spear",
            options: [TestHelpers.Equipment("pike", 1.5, perModel: true, replaces: "spear")]);

        var config = new UnitConfiguration(unit);
        Assert.Contains(CloseCombatWeaponKey.Spear, config.EffectiveWeapons);
        Assert.DoesNotContain(CloseCombatWeaponKey.Pike, config.EffectiveWeapons);

        config.Selections.Toggle("pike", "pike", true);
        Assert.DoesNotContain(CloseCombatWeaponKey.Spear, config.EffectiveWeapons);
        Assert.Contains(CloseCombatWeaponKey.Pike, config.EffectiveWeapons);
    }

    [Fact]
    public void ReplaceChoice_PointsCostCorrect()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, spear",
            options: [TestHelpers.Equipment("pike", 1.5, perModel: true, replaces: "spear")]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Selections.Toggle("pike", "pike", true);
        Assert.Equal(15, config.UpgradeCost); // 1.5 * 10
    }

    [Fact]
    public void AChoiceNamingTwoItems_GrantsBoth()
    {
        var unit = MakeUnit(equipment: "Hand weapon",
            options: [TestHelpers.Equipment("Spear and shield", 2, perModel: true)]);

        var config = new UnitConfiguration(unit);
        config.Selections.Toggle("spear-and-shield", "spear-and-shield", true);

        Assert.Contains(CloseCombatWeaponKey.Spear, config.EffectiveWeapons);
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
    }

    // ─── Count groups ────────────────────────────────────────────

    [Fact]
    public void ACountChoice_CostsItsPointsPerTake()
    {
        var group = new OptionGroup
        {
            Id = "genies", Kind = "upgrade", Select = "count", Min = 0, Max = null,
            Raw = "A Magus may be accompanied by up to two Genies +85 points/Genie",
            Choices = [new OptionChoice { Id = "genies", Name = "Genies", Points = 85, Per = "Genie", Min = 0, Max = 2, Raw = "Genies" }],
        };
        var unit = MakeUnit(points: 100, troopType: "Infantry (Character)", options: [group]);
        var config = new UnitConfiguration(unit);

        Assert.Equal(2, config.MaxCountOf(group.Choices[0]));

        config.Selections.Set("genies", "genies", 2);
        Assert.Equal(170, config.UpgradeCost);
        Assert.Equal(270, config.TotalPointsCost);
    }

    [Fact]
    public void ACountChoice_ScalingWithTheUnit_IsCappedByItsSize()
    {
        var choice = new OptionChoice { Id = "fanatic", Name = "Fanatic", Points = 25, EveryModels = 10, Raw = "Fanatic" };
        var group = new OptionGroup { Id = "fanatics", Kind = "upgrade", Select = "count", Raw = "one Fanatic for every 10 models", Choices = [choice] };
        var unit = MakeUnit(options: [group]);

        Assert.Equal(2, new UnitConfiguration(unit) { ModelCount = 25 }.MaxCountOf(choice));
        Assert.Equal(0, new UnitConfiguration(unit) { ModelCount = 9 }.MaxCountOf(choice));
    }

    // ─── Nested groups ───────────────────────────────────────────

    [Fact]
    public void ANestedGroup_OnlyCountsOnceItsParentIsTaken()
    {
        var pistol = TestHelpers.Equipment("pistol", 3);
        var leader = TestHelpers.Command(CommandRole.Champion, 5);
        leader = leader with { Choices = [leader.Choices[0] with { Options = [pistol] }] };
        var unit = MakeUnit(points: 5, options: [leader]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Selections.Toggle("pistol", "pistol", true);

        // Nobody is the champion yet, so the champion's pistol is not in force.
        Assert.Equal(50, config.TotalPointsCost);
        Assert.DoesNotContain(config.ActiveGroups(), g => g.Id == "pistol");

        config.HasChampion = true;
        Assert.Contains(config.ActiveGroups(), g => g.Id == "pistol");
        Assert.Equal(50 + 5 + 3, config.TotalPointsCost);
    }

    // ─── Command ─────────────────────────────────────────────────

    [Fact]
    public void CommandGroup_DetectedFromTypedGroups()
    {
        var unit = MakeUnit(options:
        [
            TestHelpers.Command(CommandRole.Champion, 5),
            TestHelpers.Command(CommandRole.Musician, 5),
            TestHelpers.Command(CommandRole.StandardBearer, 10),
        ]);

        var config = new UnitConfiguration(unit);
        Assert.True(config.CanHaveChampion);
        Assert.True(config.CanHaveMusician);
        Assert.True(config.CanHaveStandardBearer);

        var (champ, mus, std) = config.CommandGroupCosts;
        Assert.Equal(5, champ);
        Assert.Equal(5, mus);
        Assert.Equal(10, std);
    }

    [Fact]
    public void CommandGroup_CostIncludedInTotal()
    {
        var unit = MakeUnit(points: 8, options:
        [
            TestHelpers.Command(CommandRole.Champion, 5),
            TestHelpers.Command(CommandRole.Musician, 5),
        ]);

        var config = new UnitConfiguration(unit)
        {
            ModelCount = 10,
            HasChampion = true,
            HasMusician = true,
        };

        // 10 * 8 = 80 base + 5 champion + 5 musician = 90
        Assert.Equal(90, config.TotalPointsCost);
    }

    [Fact]
    public void MultipleUpgrades_CostsStack()
    {
        var unit = MakeUnit(
            points: 6,
            equipment: "Hand weapon, light armour",
            options:
            [
                TestHelpers.Equipment("shield", 1, perModel: true),
                TestHelpers.Equipment("melee-weapon", SelectMode.One,
                    TestHelpers.Choice("spear", 1, perModel: true),
                    TestHelpers.Choice("great weapon", 2, perModel: true)),
                TestHelpers.Command(CommandRole.Musician, 5),
            ]);

        var config = new UnitConfiguration(unit)
        {
            ModelCount = 20,
            HasMusician = true,
        };

        config.Selections.Toggle("shield", "shield", true);              // shield: +1/model
        config.Selections.SelectOne("melee-weapon", "great-weapon");    // great weapon: +2/model

        // Base: 20 * 6 = 120, shield 20, great weapon 40, musician 5 → 185
        Assert.Equal(185, config.TotalPointsCost);
    }

    [Fact]
    public void NullPointsChoice_TreatedAsZero()
    {
        var unit = MakeUnit(options: [TestHelpers.Equipment("bow", null, replaces: "hand weapon")]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.Selections.Toggle("bow", "bow", true);
        Assert.Equal(0, config.UpgradeCost);
    }

    // ─── Profiles and mounts ─────────────────────────────────────

    [Fact]
    public void AGroupNamingAProfile_IsOnlyInForceForThatProfile()
    {
        var general = new ProfileEntry { Id = "general", Name = "General", Points = 95, S = 4 };
        var captain = new ProfileEntry { Id = "captain", Name = "Captain", Points = 55, S = 4 };
        var fullPlate = TestHelpers.Equipment("full plate", 10) with { AppliesTo = "general" };
        var unit = TestHelpers.MakeUnit(name: "Commanders", troopType: "Infantry (Character)",
            profiles: [general, captain], options: [fullPlate]);

        var config = new UnitConfiguration(unit);
        Assert.Contains(config.ActiveGroups(), g => g.Id == "full-plate");

        config.SelectedProfileId = "captain";
        Assert.DoesNotContain(config.ActiveGroups(), g => g.Id == "full-plate");
        Assert.Equal("Captain", config.PrimaryProfile.Name);
    }

    [Fact]
    public void AChosenMount_SuppliesTheMountProfileAndTroopType()
    {
        var repo = TestHelpers.Repo;
        var horse = new ProfileEntry { Id = "warhorse", Name = "Warhorse", M = 8, WS = 3, S = 3, T = 3, W = 1, I = 3, A = 1 };
        var mountModel = new Model
        {
            Name = "Warhorse", Stats = horse,
            TroopType = TroopTypeInfo.Parse("Cavalry (Character)", repo.GetTroopTypeRules),
            SpecialRules = new ParsedSpecialRules(), Weapons = [], Armour = [],
        };
        var mounts = new Dictionary<string, MountOption>
        {
            ["warhorse"] = new("warhorse", mountModel, mountModel.TroopType),
        };
        var unit = TestHelpers.MakeUnit(name: "Captain", troopType: "Infantry (Character)", points: 55,
            options: [TestHelpers.Mounts(("Warhorse", 15, "warhorse"), ("Pegasus", 25, null))],
            mountOptions: mounts);

        var config = new UnitConfiguration(unit);
        Assert.Null(config.MountProfile);
        Assert.False(config.HasMount);
        Assert.Equal(BaseTroopType.Infantry, config.TroopType.BaseType);

        config.Selections.SelectOne("mount", "warhorse");
        Assert.Equal("Warhorse", config.MountProfile!.Name);
        Assert.True(config.HasMount);
        Assert.Equal(BaseTroopType.Cavalry, config.TroopType.BaseType);
        Assert.True(config.TroopType.IsCharacter);
        Assert.Equal(70, config.TotalPointsCost);

        // A mount the book only describes in prose still costs its points, without a profile.
        config.Selections.SelectOne("mount", "pegasus");
        Assert.Null(config.MountProfile);
        Assert.Equal(80, config.TotalPointsCost);
    }

    [Fact]
    public void CopySelectionsFrom_CarriesEveryChoice()
    {
        var unit = MakeUnit(points: 8, options:
        [
            TestHelpers.Equipment("shield", 1, perModel: true),
            TestHelpers.Command(CommandRole.Musician, 5),
        ]);
        var source = new UnitConfiguration(unit) { ModelCount = 20, HasMusician = true };
        source.Selections.Toggle("shield", "shield", true);
        source.Loadout.CustomName = "The Emerald Guard";
        source.Loadout.IsGeneral = true;

        var clone = new UnitConfiguration(unit);
        clone.CopySelectionsFrom(source, includeIdentity: false);

        Assert.Equal(source.TotalPointsCost, clone.TotalPointsCost);
        Assert.True(clone.Selections.IsSelected("shield", "shield"));
        Assert.Null(clone.Loadout.CustomName);
        Assert.False(clone.Loadout.IsGeneral);
    }
}
