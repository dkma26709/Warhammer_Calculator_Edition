using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

public class TroopTypeTests
{
    [Fact]
    public void Parse_Infantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Infantry", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Infantry, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
        Assert.Equal(1, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
        Assert.False(info.IsCharacter);
    }

    [Fact]
    public void Parse_LargeInfantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Large Infantry", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.LargeInfantry, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(4, info.MinRankWidth);
        Assert.Equal(2, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_MonstrousInfantry_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monstrous Infantry", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.MonstrousInfantry, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        Assert.Equal(2, info.MaxSupportingAttacks);
        Assert.False(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_Cavalry_ReturnsSplitProfile()
    {
        var info = TroopTypeInfo.Parse("Cavalry", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Cavalry, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
        Assert.True(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_MonstrousCavalry_ReturnsSplitProfile()
    {
        var info = TroopTypeInfo.Parse("Monstrous Cavalry", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.MonstrousCavalry, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        // wap-v2: "one supporting attack from the rider, and two from the mount". wap-v3 keeps
        // the rider's one and takes the mount's away — see TroopTypeBySystemTests.
        Assert.Equal(1, info.MaxSupportingAttacks);
        Assert.Equal(2, info.MountSupportingAttacks);
        Assert.Equal(3, info.NonSplitSupportingAttacks);
        Assert.True(info.HasSplitProfile);
    }

    [Fact]
    public void Parse_WarBeasts_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("War Beasts", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.WarBeasts, info.BaseType);
        Assert.Equal(1, info.UnitStrength);
        Assert.Equal(5, info.MinRankWidth);
    }

    [Fact]
    public void Parse_MonstrousBeast_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monstrous Beast", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.MonstrousBeasts, info.BaseType);
        Assert.Equal(2, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
    }

    [Fact]
    public void Parse_Swarms_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Swarms", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Swarms, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(3, info.MinRankWidth);
        Assert.Equal(5, info.MaxSupportingAttacks);
    }

    [Fact]
    public void Parse_Monster_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Monster", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Monster, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(1, info.MinRankWidth);
        Assert.Equal(0, info.MaxSupportingAttacks);
    }

    [Fact]
    public void Parse_Chariot_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Chariot", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Chariot, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
    }

    [Fact]
    public void Parse_Shrine_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("Shrine", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Shrine, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
    }

    [Fact]
    public void Parse_Typo_MosntrousBeast_IsCorrected()
    {
        var info = TroopTypeInfo.Parse("Mosntrous Beast", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.MonstrousBeasts, info.BaseType);
    }

    [Fact]
    public void Parse_CharacterFlag_SetsIsCharacter()
    {
        var info = TroopTypeInfo.Parse("Infantry (Character, Human)", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Infantry, info.BaseType);
        Assert.True(info.IsCharacter);
    }

    [Fact]
    public void Parse_ArmourSaveFlag_SetsIntrinsicArmour()
    {
        var info = TroopTypeInfo.Parse("Chariot (Armour Save 4+)", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Chariot, info.BaseType);
        Assert.Equal(4, info.IntrinsicArmourSave);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsDefaultInfantry()
    {
        var info1 = TroopTypeInfo.Parse(null, TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Infantry, info1.BaseType);

        var info2 = TroopTypeInfo.Parse("", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Infantry, info2.BaseType);

        var info3 = TroopTypeInfo.Parse("  ", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.Infantry, info3.BaseType);
    }

    [Fact]
    public void Parse_WarMachine_ReturnsCorrectProperties()
    {
        var info = TroopTypeInfo.Parse("War Machine", TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));
        Assert.Equal(BaseTroopType.WarMachine, info.BaseType);
        Assert.Equal(3, info.UnitStrength);
        Assert.Equal(1, info.MinRankWidth);
    }

    [Theory]
    [InlineData("Monster (Hydra) \nHANDLERS: 2 Beastmaster Apprentices (Dark Elf)", BaseTroopType.Monster)]
    [InlineData("Monstrous Infantry (Skaven, Clan Moulder) \nHANDLERS: 1 Packmaster (Skaven)",
        BaseTroopType.MonstrousInfantry)]
    [InlineData("Monster (Daemon) \nHANDLER: 3 (Chaos Dwarf)", BaseTroopType.Monster)]
    [InlineData("War Beast (Saurian) \nHANDLER: 3 Skinks (Lizardman, Skink)", BaseTroopType.WarBeasts)]
    public void Parse_HandlerLineAfterANewline_ReadsTheFirstLineOnly(string raw, BaseTroopType expected)
    {
        // The pattern cannot cross a newline, so the whole match used to fail and these fell back
        // to Infantry with unit strength 1 — a War Hydra fighting as a footsoldier.
        var info = TroopTypeInfo.Parse(raw, TestHelpers.TroopTypeRulesFor(GameSystems.WapV2));

        Assert.Equal(expected, info.BaseType);
    }

    /// <summary>
    /// The shipped data rather than a hand-typed string: every unit in both systems must resolve
    /// to the base type its own first line names. This is the guard that would have caught the
    /// newline, which no unit test written against a tidy string could.
    /// </summary>
    [Theory]
    [MemberData(nameof(FactionDeserializationTests.SystemFactions), MemberType = typeof(FactionDeserializationTests))]
    public void EveryShippedTroopType_ResolvesToItsOwnFirstLine(string systemKey, string faction)
    {
        Assert.True(FactionKeyExtensions.TryFromJsonKey(faction, out var factionKey), faction);

        var repo = TestHelpers.RepoFor(systemKey);
        var units = repo.GetUnitsAsync(factionKey).GetAwaiter().GetResult();

        var wrong = new List<string>();
        foreach (var unit in units)
        {
            var raw = unit.RawEntry.TroopType;
            if (string.IsNullOrWhiteSpace(raw) || !raw.Contains('\n')) continue;

            var expected = TroopTypeInfo.Parse(
                raw.Split('\n')[0], TestHelpers.TroopTypeRulesFor(systemKey)).BaseType;

            if (unit.BaseModel.TroopType.BaseType != expected)
                wrong.Add($"{unit.Name}: expected {expected}, got {unit.BaseModel.TroopType.BaseType}");
        }

        Assert.Empty(wrong);
    }
}
