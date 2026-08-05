using Warhammer.Services;

namespace Warhammer.Tests;

public class CoreRulesServiceTests
{
    [Theory]
    [InlineData(4, 4, 4)] // Equal WS
    [InlineData(3, 4, 4)] // Lower WS
    [InlineData(4, 3, 3)] // Higher WS
    [InlineData(1, 10, 6)] // Minimum vs Maximum (per table)
    public void GetToHitRequirement_ReturnsExpected(int attackerWS, int defenderWS, int expected)
    {
        var service = TestHelpers.CreateCoreRules();
        var requirement = service.GetToHitRequirement(attackerWS, defenderWS);

        Assert.Equal(expected, requirement.FirstRoll);
        Assert.False(requirement.NeedsSecondRoll, "to-hit charts never carry a confirming roll");
    }

    [Theory]
    [InlineData(4, 3, 3)] // S > T
    [InlineData(3, 3, 4)] // S == T
    [InlineData(3, 4, 5)] // S < T
    [InlineData(3, 5, 6)] // S << T
    [InlineData(6, 3, 2)] // S >> T
    public void GetToWoundRequirement_ReturnsExpected(int strength, int toughness, int expected)
    {
        var service = TestHelpers.CreateCoreRules();
        var requirement = service.GetToWoundRequirement(strength, toughness);

        // TestHelpers loads wap-v2, whose to-wound chart is entirely plain scores.
        Assert.Equal(expected, requirement.FirstRoll);
        Assert.False(requirement.NeedsSecondRoll);
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

}
