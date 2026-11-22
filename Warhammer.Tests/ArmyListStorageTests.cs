using Warhammer.Services;

namespace Warhammer.Tests;

public class ArmyListStorageTests
{
    [Fact]
    public void ShelvedKey_IsApartFromTheKeysTheBuilderWrites()
    {
        // A shelf under the autosave or print key would be overwritten by the next edit or
        // print, which is what setting a list aside exists to prevent.
        var shelved = ArmyListStorage.ShelvedKey("war");

        Assert.NotEqual(ArmyListStorage.ActiveKey("war"), shelved);
        Assert.NotEqual(ArmyListStorage.PrintKey("war"), shelved);
        Assert.NotEqual(ArmyListStorage.LegacyActiveKey, shelved);
    }

    [Fact]
    public void ShelvedKey_IsPerSystem()
    {
        Assert.NotEqual(ArmyListStorage.ShelvedKey("war"), ArmyListStorage.ShelvedKey("wap-v2"));
    }
}
