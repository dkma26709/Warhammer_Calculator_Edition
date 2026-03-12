using Warhammer.Services;

namespace Warhammer.Tests;

public class CoreRulesServiceTests
{
    [Theory]
    [InlineData(4, 4, 4)] // Equal WS
    [InlineData(3, 4, 4)] // Lower WS
    [InlineData(4, 3, 3)] // Higher WS
    [InlineData(1, 10, 6)] // Minimum vs Maximum (per table)
    public void GetToHitScore_ReturnsExpected(int attackerWS, int defenderWS, int expected)
    {
        var service = TestHelpers.CreateCoreRules();
        Assert.Equal(expected, service.GetToHitScore(attackerWS, defenderWS));
    }

    [Theory]
    [InlineData(4, 3, 3)] // S > T
    [InlineData(3, 3, 4)] // S == T
    [InlineData(3, 4, 5)] // S < T
    [InlineData(3, 5, 6)] // S << T
    [InlineData(6, 3, 2)] // S >> T
    public void GetToWoundScore_ReturnsExpected(int strength, int toughness, int expected)
    {
        var service = TestHelpers.CreateCoreRules();
        Assert.Equal(expected, service.GetToWoundScore(strength, toughness));
    }

    [Theory]
    [InlineData(3, 0)]  // S3: no modifier
    [InlineData(4, -1)] // S4: -1
    [InlineData(5, -2)] // S5: -2
    [InlineData(6, -3)] // S6: -3
    public void GetArmourSaveModifier_ReturnsExpected(int strength, int expected)
    {
        var service = TestHelpers.CreateCoreRules();
        Assert.Equal(expected, service.GetArmourSaveModifier(strength));
    }

    [Fact]
    public void ParseWeaponStrength_AsUser()
    {
        Assert.Equal(4, CoreRulesService.ParseWeaponStrength("As user", 4));
    }

    [Fact]
    public void ParseWeaponStrength_Plus()
    {
        Assert.Equal(5, CoreRulesService.ParseWeaponStrength("+1", 4));
    }

    [Fact]
    public void ParseWeaponStrength_Fixed()
    {
        Assert.Equal(6, CoreRulesService.ParseWeaponStrength("6", 4));
    }

    [Fact]
    public void ParseArmourSave_Standard()
    {
        var (mod, save) = CoreRulesService.ParseArmourSave("+3/4+");
        Assert.Equal(3, mod);
        Assert.Equal(4, save);
    }

    [Fact]
    public void ParseArmourSave_Light()
    {
        var (mod, save) = CoreRulesService.ParseArmourSave("+1/6+");
        Assert.Equal(1, mod);
        Assert.Equal(6, save);
    }
}
