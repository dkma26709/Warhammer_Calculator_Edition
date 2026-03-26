namespace Warhammer.Calculator;

public enum FleeResult { Caught, Escaped }

public static class FleeAndPursueResolver
{
    /// <summary>
    /// Resolve flee vs pursue after a unit breaks.
    /// If pursue distance >= flee distance, the broken unit is caught and destroyed.
    /// </summary>
    /// <param name="fleerHasSwiftstride">Whether the fleeing unit has Swiftstride (from rules or troop type).</param>
    /// <param name="pursuerHasSwiftstride">Whether the pursuing unit has Swiftstride.</param>
    /// <param name="rng">Random number generator.</param>
    public static FleeResult Resolve(bool fleerHasSwiftstride, bool pursuerHasSwiftstride, Random rng)
    {
        int fleeDistance = fleerHasSwiftstride ? Roll3D6DropLowest(rng) : Roll2D6(rng);
        int pursueDistance = pursuerHasSwiftstride ? Roll3D6DropLowest(rng) : Roll2D6(rng);

        return pursueDistance >= fleeDistance ? FleeResult.Caught : FleeResult.Escaped;
    }

    private static int Roll2D6(Random rng) => rng.Next(1, 7) + rng.Next(1, 7);

    private static int Roll3D6DropLowest(Random rng)
    {
        int d1 = rng.Next(1, 7);
        int d2 = rng.Next(1, 7);
        int d3 = rng.Next(1, 7);
        return d1 + d2 + d3 - Math.Min(d1, Math.Min(d2, d3));
    }
}
