using Warhammer.Calculator.Models;
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

    // ─── Factions are ids, not an enum ──────────────────────────────

    [Theory]
    [InlineData("high-elves", true)]
    [InlineData("dark-elves", true)]
    [InlineData("wood-elves", true)]
    [InlineData("empire", false)]
    [InlineData("orcs-goblins", false)]
    [InlineData(null, false)]
    public void FactionRaces_IsElf(string? factionId, bool expected)
    {
        Assert.Equal(expected, FactionRaces.IsElf(factionId));
    }

    [Theory]
    [InlineData("Orcs", "orcs-goblins", true)]
    [InlineData("Orcs, Goblins", "hobgoblins", true)]
    [InlineData("Dwarfs", "chaos-dwarfs", true)]
    [InlineData("Elves", "empire", false)]
    [InlineData("Undead", "zombie-pirates", true)]
    public void FactionRaces_HatredTargetsMatchTheRaceTable(string target, string factionId, bool expected)
    {
        Assert.Equal(expected, FactionRaces.HatredApplies(target, factionId));
    }

    [Fact]
    public void FactionRaces_KnowsEveryFactionTheBundleShips()
    {
        var known = FactionRaces.KnownFactions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var faction in TestHelpers.Repo.AllFactions)
            Assert.Contains(faction.Id, known);
    }

    // ─── Categories ─────────────────────────────────────────────────

    [Theory]
    [InlineData(UnitCategory.Core, "Core")]
    [InlineData(UnitCategory.Lords, "Lords")]
    [InlineData(UnitCategory.CharacterMounts, "Character Mounts")]
    public void UnitCategory_RoundTrips(UnitCategory cat, string jsonKey)
    {
        Assert.Equal(jsonKey, cat.ToJsonKey());
        Assert.True(UnitCategoryExtensions.TryFromJsonKey(jsonKey, out var parsed));
        Assert.Equal(cat, parsed);
    }

    [Theory]
    [InlineData("characters", null, false, UnitCategory.Characters)]
    [InlineData("characters", "lord", false, UnitCategory.Lords)]
    [InlineData("characters", "hero", false, UnitCategory.Heroes)]
    [InlineData("characters", "lord", true, UnitCategory.NamedCharacters)]
    [InlineData("core", null, false, UnitCategory.Core)]
    [InlineData("special", null, false, UnitCategory.Special)]
    [InlineData("rare", null, false, UnitCategory.Rare)]
    [InlineData("mounts", null, false, UnitCategory.CharacterMounts)]
    [InlineData(null, null, false, null)]
    [InlineData("bestiary", null, false, null)]
    public void UnitCategory_FromTheBundlesSectionTierAndNamed(string? section, string? tier, bool named, UnitCategory? expected)
    {
        Assert.Equal(expected, UnitCategoryExtensions.FromSection(section, tier, named));
    }

    [Theory]
    [InlineData("champion", CommandRole.Champion)]
    [InlineData("Leader", CommandRole.Champion)]
    [InlineData("Champion", CommandRole.Champion)]
    [InlineData("musician", CommandRole.Musician)]
    [InlineData("standardBearer", CommandRole.StandardBearer)]
    [InlineData("Standard Bearer", CommandRole.StandardBearer)]
    public void CommandRole_FromRoleText(string text, CommandRole expected)
    {
        Assert.True(CommandRoleExtensions.TryFromRoleText(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("equipment", OptionKind.Equipment)]
    [InlineData("magicItems", OptionKind.MagicItems)]
    [InlineData("MAGICSTANDARD", OptionKind.MagicStandard)]
    [InlineData("factionUpgrade", OptionKind.FactionUpgrade)]
    public void OptionKind_FromTheBundlesKey(string key, OptionKind expected)
    {
        Assert.True(OptionKindExtensions.TryFromJsonKey(key, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(expected, OptionKindExtensions.TryFromJsonKey(expected.ToJsonKey(), out var back) ? back : (OptionKind?)null);
    }

    [Fact]
    public void OptionKind_UnknownKey_ReturnsFalse()
    {
        Assert.False(OptionKindExtensions.TryFromJsonKey("teleporter", out _));
        Assert.False(OptionKindExtensions.TryFromJsonKey(null, out _));
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

    [Fact]
    public void CommandAvailability_FromGroups_ReadsRolesAndCosts()
    {
        var command = CommandAvailability.FromGroups(
        [
            TestHelpers.Command(CommandRole.Champion, 5),
            TestHelpers.Command(CommandRole.StandardBearer, 10),
        ]);

        Assert.True(command.CanHaveChampion);
        Assert.Equal(5, command.ChampionCost);
        Assert.False(command.CanHaveMusician);
        Assert.True(command.CanHaveStandardBearer);
        Assert.Equal(10, command.StandardBearerCost);
    }
}
