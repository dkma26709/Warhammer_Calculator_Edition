namespace Warhammer.Calculator;

/// <summary>
/// Shared dice rolling utility for expressions like "D6", "D3", "D3+1", "2D6".
/// </summary>
public static class DiceRoller
{
    public static int Roll(string expr, Random rng)
    {
        var upper = expr.ToUpperInvariant().Trim();

        if (upper == "D6") return rng.Next(1, 7);
        if (upper == "D3") return rng.Next(1, 4);
        if (upper == "2D6") return rng.Next(1, 7) + rng.Next(1, 7);

        if (upper.StartsWith("D3+") && int.TryParse(upper[3..], out var d3mod))
            return rng.Next(1, 4) + d3mod;
        if (upper.StartsWith("D6+") && int.TryParse(upper[3..], out var d6mod))
            return rng.Next(1, 7) + d6mod;

        return 1;
    }
}
