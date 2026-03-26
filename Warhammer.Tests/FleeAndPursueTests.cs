using Warhammer.Calculator;

namespace Warhammer.Tests;

public class FleeAndPursueTests
{
    private const int Iterations = 5_000;

    private static double CaughtRate(bool fleerSwift, bool pursuerSwift)
    {
        int caught = 0;
        for (int seed = 0; seed < Iterations; seed++)
        {
            var result = FleeAndPursueResolver.Resolve(fleerSwift, pursuerSwift, new Random(seed));
            if (result == FleeResult.Caught) caught++;
        }
        return (double)caught / Iterations;
    }

    [Fact]
    public void BothNormal_CaughtRateAround50Percent()
    {
        // Both roll 2d6. P(pursue >= flee) ≈ 50%
        double rate = CaughtRate(fleerSwift: false, pursuerSwift: false);
        TestHelpers.AssertRate(rate, 0.43, 0.57, "Both 2d6 caught rate");
    }

    [Fact]
    public void SwiftPursuerVsNormalFleer_HigherCatchRate()
    {
        // Pursuer rolls 3d6-drop-lowest (avg ~9.5) vs fleer 2d6 (avg 7)
        double rate = CaughtRate(fleerSwift: false, pursuerSwift: true);
        Assert.True(rate > 0.55, $"Swift pursuer should catch >55%, got {rate:P1}");
    }

    [Fact]
    public void NormalPursuerVsSwiftFleer_LowerCatchRate()
    {
        // Pursuer 2d6 (avg 7) vs fleer 3d6-drop-lowest (avg ~9.5)
        double rate = CaughtRate(fleerSwift: true, pursuerSwift: false);
        Assert.True(rate < 0.45, $"Normal pursuer should catch <45% of swift fleer, got {rate:P1}");
    }

    [Fact]
    public void BothSwift_CaughtRateAround50Percent()
    {
        // Both roll 3d6-drop-lowest. Same distribution → ~50%
        double rate = CaughtRate(fleerSwift: true, pursuerSwift: true);
        TestHelpers.AssertRate(rate, 0.43, 0.57, "Both swift caught rate");
    }
}
