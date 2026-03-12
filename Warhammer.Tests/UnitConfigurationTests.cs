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
        List<UnitOption>? options = null)
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
        // Champion does NOT add +1 attack in WAP
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
    public void MountAttacks_Cavalry_WithSupportRank()
    {
        var mount = new ProfileEntry { Name = "Warhorse", M = 8, WS = 3, BS = 0, S = 3, T = 3, W = 1, I = 3, A = 1, Ld = 5 };
        var unit = MakeUnit(troopType: "Cavalry", unitSize: "10+", mountProfile: mount);
        var config = new UnitConfiguration(unit) { ModelCount = 10, Frontage = 5 };
        // 5 front * 1A = 5, 1 support rank * 5 models * 1 mount support attack = 5
        Assert.Equal(10, config.MountAttacks());
    }

    [Fact]
    public void MountAttacks_MonstrousCavalry_WithSupportRank()
    {
        var mount = new ProfileEntry { Name = "Monster", M = 7, WS = 3, BS = 0, S = 4, T = 4, W = 3, I = 2, A = 2, Ld = 5 };
        var unit = MakeUnit(troopType: "Monstrous Cavalry", unitSize: "6+", mountProfile: mount);
        var config = new UnitConfiguration(unit) { ModelCount = 6, Frontage = 3 };
        // 3 front * 2A = 6, 1 support rank * 3 models * 2 mount support = 6
        Assert.Equal(12, config.MountAttacks());
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
        var (primary, mount) = UnitConfiguration.ResolveProfiles(unit.RawEntry);
        Assert.NotNull(primary);
        Assert.Null(mount);
    }

    [Fact]
    public void ResolveProfiles_TwoProfiles_PrimaryHasPoints()
    {
        var p1 = new ProfileEntry { Name = "Rider", Points = 10 };
        var p2 = new ProfileEntry { Name = "Mount" };
        var unit = new UnitEntry { Name = "Cavalry", Profiles = [p1, p2], TroopType = "Cavalry" };
        var (primary, mount) = UnitConfiguration.ResolveProfiles(unit);
        Assert.Equal("Rider", primary.Name);
        Assert.Equal("Mount", mount!.Name);
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
    public void EffectiveStrength_WeaponMightyBlow_UsesHighest()
    {
        var unit = MakeUnit(s: 3, specialRules: "Mighty Blow (1)");
        var config = new UnitConfiguration(unit);
        var weapon = new Weapon
        {
            Key = CloseCombatWeaponKey.Flail, Name = "Flail",
            Strength = WeaponStrength.Parse("As user"), Rules = new ParsedWeaponRules(),
        };
        // Unit MB=1, Weapon MB=2 → use highest = 2
        Assert.Equal(5, config.EffectiveStrength(weapon, false, 1, weaponMightyBlow: 2));
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

    private static UnitOption MakeEquipmentOption(
        string action, List<OptionChoice> choices, string? replaces = null) =>
        new()
        {
            Type = "equipment",
            Action = action,
            Replaces = replaces,
            Choices = choices,
        };

    private static UnitOption MakeCommandOption(string role, double points) =>
        new()
        {
            Type = "command",
            Role = role,
            Points = points,
        };

    [Fact]
    public void ShieldUpgrade_AddsToEffectiveArmour()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, light armour",
            options:
            [
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["shield"], Points = 1, PerModel = true }
                ])
            ]);

        var config = new UnitConfiguration(unit);
        Assert.DoesNotContain(ArmourKey.Shields, config.EffectiveArmour);

        config.SelectedUpgrades[0] = 0;
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
        Assert.Contains(ArmourKey.LightArmour, config.EffectiveArmour);
    }

    [Fact]
    public void ShieldUpgrade_ImprovesArmourSave()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, light armour",
            options:
            [
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["shield"], Points = 1, PerModel = true }
                ])
            ]);

        var config = new UnitConfiguration(unit);

        // Without shield: light armour +1 → save 6+
        Assert.Equal(6, config.EffectiveArmourSave(3, 0, 0));

        // With shield: light armour +1 + shield +1 → save 5+
        config.SelectedUpgrades[0] = 0;
        Assert.Equal(5, config.EffectiveArmourSave(3, 0, 0));
    }

    [Fact]
    public void EquipmentUpgradeCost_PerModel()
    {
        var unit = MakeUnit(
            options:
            [
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["shield"], Points = 1, PerModel = true }
                ])
            ]);

        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        Assert.Equal(0, config.EquipmentUpgradeCost);

        config.SelectedUpgrades[0] = 0;
        Assert.Equal(20, config.EquipmentUpgradeCost);
    }

    [Fact]
    public void EquipmentUpgradeCost_FlatCost()
    {
        var unit = MakeUnit(
            options:
            [
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["heavy armour"], Points = 9, PerModel = false }
                ])
            ]);

        var config = new UnitConfiguration(unit) { ModelCount = 20 };
        config.SelectedUpgrades[0] = 0;
        Assert.Equal(9, config.EquipmentUpgradeCost);
    }

    [Fact]
    public void EquipmentUpgradeCost_IncludedInTotalPoints()
    {
        var unit = MakeUnit(
            points: 8,
            options:
            [
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["shield"], Points = 1, PerModel = true }
                ])
            ]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        Assert.Equal(80, config.TotalPointsCost); // 10 * 8 = 80

        config.SelectedUpgrades[0] = 0;
        Assert.Equal(90, config.TotalPointsCost); // 80 + 10 * 1 = 90
    }

    [Fact]
    public void ChooseArmourUpgrade_OnlyOneSelectable()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, medium armour",
            options:
            [
                MakeEquipmentOption("choose",
                [
                    new OptionChoice { Name = ["heavy armour"], Points = 9, PerModel = false },
                    new OptionChoice { Name = ["shield"], Points = 3, PerModel = false }
                ])
            ]);

        var config = new UnitConfiguration(unit);

        // Select heavy armour
        config.SelectedUpgrades[0] = 0;
        Assert.Contains(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.DoesNotContain(ArmourKey.Shields, config.EffectiveArmour);

        // Switch to shield
        config.SelectedUpgrades[0] = 1;
        Assert.DoesNotContain(ArmourKey.HeavyArmour, config.EffectiveArmour);
        Assert.Contains(ArmourKey.Shields, config.EffectiveArmour);
    }

    [Fact]
    public void ReplaceAction_RemovesBaseEquipment()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, spear",
            options:
            [
                MakeEquipmentOption("replace",
                [
                    new OptionChoice { Name = ["pike"], Points = 1.5, PerModel = true }
                ], replaces: "spear")
            ]);

        var config = new UnitConfiguration(unit);
        Assert.Contains(CloseCombatWeaponKey.Spear, config.EffectiveWeapons);
        Assert.DoesNotContain(CloseCombatWeaponKey.Pike, config.EffectiveWeapons);

        config.SelectedUpgrades[0] = 0;
        Assert.DoesNotContain(CloseCombatWeaponKey.Spear, config.EffectiveWeapons);
        Assert.Contains(CloseCombatWeaponKey.Pike, config.EffectiveWeapons);
    }

    [Fact]
    public void ReplaceAction_PointsCostCorrect()
    {
        var unit = MakeUnit(
            equipment: "Hand weapon, spear",
            options:
            [
                MakeEquipmentOption("replace",
                [
                    new OptionChoice { Name = ["pike"], Points = 1.5, PerModel = true }
                ], replaces: "spear")
            ]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.SelectedUpgrades[0] = 0;
        Assert.Equal(15, config.EquipmentUpgradeCost); // 1.5 * 10
    }

    [Fact]
    public void CommandGroup_DetectedFromTypedOptions()
    {
        var unit = MakeUnit(
            options:
            [
                MakeCommandOption("Leader", 5),
                MakeCommandOption("Musician", 5),
                MakeCommandOption("Standard Bearer", 10),
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
        var unit = MakeUnit(
            points: 8,
            options:
            [
                MakeCommandOption("Leader", 5),
                MakeCommandOption("Musician", 5),
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
                MakeEquipmentOption("add",
                [
                    new OptionChoice { Name = ["shield"], Points = 1, PerModel = true }
                ]),
                MakeEquipmentOption("choose",
                [
                    new OptionChoice { Name = ["spear"], Points = 1, PerModel = true },
                    new OptionChoice { Name = ["great weapon"], Points = 2, PerModel = true }
                ]),
                MakeCommandOption("Musician", 5),
            ]);

        var config = new UnitConfiguration(unit)
        {
            ModelCount = 20,
            HasMusician = true,
        };

        // Select shield + great weapon
        config.SelectedUpgrades[0] = 0; // shield: +1/model
        config.SelectedUpgrades[1] = 1; // great weapon: +2/model

        // Base: 20 * 6 = 120
        // Shield: 20 * 1 = 20
        // Great weapon: 20 * 2 = 40
        // Musician: 5
        // Total: 185
        Assert.Equal(185, config.TotalPointsCost);
    }

    [Fact]
    public void NullPointsChoice_TreatedAsZero()
    {
        var unit = MakeUnit(
            options:
            [
                MakeEquipmentOption("replace",
                [
                    new OptionChoice { Name = ["bow"], Points = null, PerModel = false }
                ], replaces: "hand weapon")
            ]);

        var config = new UnitConfiguration(unit) { ModelCount = 10 };
        config.SelectedUpgrades[0] = 0;
        Assert.Equal(0, config.EquipmentUpgradeCost);
    }
}
