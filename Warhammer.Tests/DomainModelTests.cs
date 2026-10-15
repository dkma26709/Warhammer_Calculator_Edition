using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

public class DomainModelTests
{
    // ─── Enum Round-Tripping ────────────────────────────────────────

    [Theory]
    [InlineData(CloseCombatWeaponKey.HandWeapon, "handWeapon")]
    [InlineData(CloseCombatWeaponKey.GreatWeapon, "greatWeapon")]
    [InlineData(CloseCombatWeaponKey.Spear, "spear")]
    [InlineData(CloseCombatWeaponKey.Flail, "flail")]
    [InlineData(CloseCombatWeaponKey.TwoAdditionalHandWeapons, "two_additionalHandWeapons")]
    public void CloseCombatWeaponKey_RoundTrips(CloseCombatWeaponKey key, string jsonKey)
    {
        Assert.Equal(jsonKey, key.ToJsonKey());
        Assert.True(CloseCombatWeaponKeyExtensions.TryFromJsonKey(jsonKey, out var parsed));
        Assert.Equal(key, parsed);
    }

    [Fact]
    public void CloseCombatWeaponKey_InvalidKey_ReturnsFalse()
    {
        Assert.False(CloseCombatWeaponKeyExtensions.TryFromJsonKey("laserSword", out _));
    }

    [Theory]
    [InlineData(ArmourKey.LightArmour, "lightArmour")]
    [InlineData(ArmourKey.HeavyArmour, "heavyArmour")]
    [InlineData(ArmourKey.Shields, "shields")]
    [InlineData(ArmourKey.Barding, "barding")]
    public void ArmourKey_RoundTrips(ArmourKey key, string jsonKey)
    {
        Assert.Equal(jsonKey, key.ToJsonKey());
        Assert.True(ArmourKeyExtensions.TryFromJsonKey(jsonKey, out var parsed));
        Assert.Equal(key, parsed);
    }

    [Theory]
    [InlineData(FactionKey.TheEmpire, "the-empire", "The Empire")]
    [InlineData(FactionKey.DarkElves, "dark-elves", "Dark Elves")]
    [InlineData(FactionKey.OrcsAndGoblins, "orcs-and-goblins", "Orcs & Goblins")]
    public void FactionKey_JsonKeyAndDisplayName(FactionKey key, string jsonKey, string displayName)
    {
        Assert.Equal(jsonKey, key.ToJsonKey());
        Assert.Equal(displayName, key.ToDisplayName());
        Assert.True(FactionKeyExtensions.TryFromJsonKey(jsonKey, out var parsed));
        Assert.Equal(key, parsed);
    }

    [Theory]
    [InlineData("vampire-counts", "Vampire Counts")]
    [InlineData("orcs-and-goblins", "Orcs & Goblins")]
    [InlineData("no-such-faction", "no-such-faction")]
    [InlineData(null, "—")]
    public void FactionKey_DisplayNameForJsonKey(string? jsonKey, string expected)
    {
        Assert.Equal(expected, FactionKeyExtensions.DisplayNameForJsonKey(jsonKey));
    }

    [Fact]
    public void FactionKey_AllFactions_Returns30()
    {
        // Every army of every system: wap-v2 and wap-v3 list 15 of them, WAR all 30.
        Assert.Equal(30, FactionKeyExtensions.All.Count);
    }

    [Theory]
    [InlineData(FactionKey.HighElves, true)]
    [InlineData(FactionKey.DarkElves, true)]
    [InlineData(FactionKey.WoodElves, true)]
    [InlineData(FactionKey.TheEmpire, false)]
    [InlineData(FactionKey.OrcsAndGoblins, false)]
    public void FactionKey_IsElf(FactionKey key, bool expected)
    {
        Assert.Equal(expected, key.IsElf());
    }

    [Theory]
    [InlineData(UnitCategory.Core, "Core")]
    [InlineData(UnitCategory.Lords, "Lords")]
    [InlineData(UnitCategory.CharacterMounts, "Character Mounts")]
    public void UnitCategory_RoundTrips(UnitCategory cat, string jsonKey)
    {
        Assert.True(UnitCategoryExtensions.TryFromJsonKey(jsonKey, out var parsed));
        Assert.Equal(cat, parsed);
    }

    [Theory]
    [InlineData("Leader", CommandRole.Champion)]
    [InlineData("Champion", CommandRole.Champion)]
    [InlineData("Musician", CommandRole.Musician)]
    [InlineData("Standard Bearer", CommandRole.StandardBearer)]
    public void CommandRole_FromRoleText(string text, CommandRole expected)
    {
        Assert.True(CommandRoleExtensions.TryFromRoleText(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    // ─── WeaponStrength Parsing ─────────────────────────────────────

    [Fact]
    public void WeaponStrength_AsUser()
    {
        var ws = WeaponStrength.Parse("As user");
        Assert.IsType<WeaponStrength.AsUser>(ws);
        Assert.Equal(4, ws.Resolve(4));
    }

    [Fact]
    public void WeaponStrength_Modifier()
    {
        var ws = WeaponStrength.Parse("+2");
        var mod = Assert.IsType<WeaponStrength.Modifier>(ws);
        Assert.Equal(2, mod.Value);
        Assert.Equal(6, ws.Resolve(4));
    }

    [Fact]
    public void WeaponStrength_Fixed()
    {
        var ws = WeaponStrength.Parse("5");
        var fix = Assert.IsType<WeaponStrength.Fixed>(ws);
        Assert.Equal(5, fix.Value);
        Assert.Equal(5, ws.Resolve(3));
    }

    // ─── Armour Parsing ─────────────────────────────────────────────

    [Theory]
    [InlineData("+1/6+", 1, 6)]
    [InlineData("+3/4+", 3, 4)]
    [InlineData("4+", 0, 7)]
    public void Armour_ParseSaveField(string input, int expectedMod, int expectedStandaloneSave)
    {
        var (mod, standaloneSave) = Models.Domain.Armour.ParseSaveField(input);
        Assert.Equal(expectedMod, mod);
        Assert.Equal(expectedStandaloneSave, standaloneSave);
    }

    [Theory]
    [InlineData("Parry (6+)", 6)]
    [InlineData("Parry (5+)", 5)]
    [InlineData(null, null)]
    [InlineData("Some other rule", null)]
    public void Armour_ParseParryValue(string? input, int? expected)
    {
        Assert.Equal(expected, Models.Domain.Armour.ParseParryValue(input));
    }

    // ─── CommandAvailability ────────────────────────────────────────

    [Fact]
    public void CommandAvailability_None_HasNoOptions()
    {
        var none = CommandAvailability.None;
        Assert.False(none.CanHaveChampion);
        Assert.False(none.CanHaveMusician);
        Assert.False(none.CanHaveStandardBearer);
    }
}
