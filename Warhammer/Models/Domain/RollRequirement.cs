namespace Warhammer.Models.Domain;

/// <summary>
/// What a D6 must score to succeed on a combat chart — used for both the to-hit and the
/// to-wound lookups, so the two charts are read and modified the same way.
/// </summary>
/// <remarks>
/// A bare score covers every to-hit cell in both game systems and every to-wound cell in
/// wap-v2. wap-v3's to-wound chart adds two shapes a plain int cannot carry: a compound
/// result needing a confirming second die, and outright immunity.
///
/// From the wap-v3 rulebook (Roll To Wound): "In the boxes where there are two values listed,
/// this means that in order to successfully score a Wound, you must first roll a 6, followed
/// by another D6. In situations where the roll might have a modifier to it, this primarily
/// affects the second roll before the first. For example, a Strength 2 Attack with a +1
/// modifier treats Toughness 5 as only requiring to roll a 6+ To Wound, rather than a 6+
/// followed by a 2+."
///
/// That makes a chart one ladder, easiest to hardest:
/// <c>2+ 3+ 4+ 5+ 6+ 6+/2+ 6+/3+ 6+/4+ 6+/5+ 6+/6+ n/a</c>. A modifier is a step along it,
/// which is why <see cref="Step"/> spends the second roll before touching the first.
/// </remarks>
public readonly record struct RollRequirement
{
    /// <summary>Score needed on the first D6, or null when the roll cannot succeed at all.</summary>
    public int? FirstRoll { get; }

    /// <summary>Score needed on a confirming second D6, or null when one roll settles it.</summary>
    public int? SecondRoll { get; }

    private RollRequirement(int? firstRoll, int? secondRoll)
    {
        FirstRoll = firstRoll;
        SecondRoll = secondRoll;
    }

    /// <summary>A single-roll requirement, e.g. 4+.</summary>
    public static RollRequirement Plain(int score) => new(Math.Clamp(score, 2, 6), null);

    /// <summary>A 6 followed by a confirming roll, e.g. 6+/3+.</summary>
    public static RollRequirement Compound(int secondRoll) => new(6, Math.Clamp(secondRoll, 2, 6));

    /// <summary>The roll cannot succeed — the chart's "n/a".</summary>
    public static RollRequirement Impossible { get; } = new(null, null);

    public bool CanSucceed => FirstRoll.HasValue;
    public bool NeedsSecondRoll => SecondRoll.HasValue;

    /// <summary>Rungs from the easiest requirement, 2+, so ladder maths stays in one place.</summary>
    private int LadderIndex
    {
        get
        {
            if (FirstRoll is not { } first) return 10;             // n/a sits past 6+/6+
            if (SecondRoll is { } second) return 4 + second - 1;   // 6+/2+ = 5 … 6+/6+ = 9
            return first - 2;                                       // 2+ = 0 … 6+ = 4
        }
    }

    private static RollRequirement FromLadderIndex(int index) => index switch
    {
        <= 0 => Plain(2),
        <= 4 => Plain(index + 2),
        <= 9 => Compound(index - 3),
        _ => Impossible,
    };

    /// <summary>
    /// Move along the ladder. Negative is easier, matching
    /// <see cref="UnitModifier.ToWoundModifier"/>: -1 means "needs one lower".
    /// </summary>
    public RollRequirement Step(int steps) =>
        steps == 0 ? this : FromLadderIndex(LadderIndex + steps);

    /// <summary>Chance a single attempt succeeds, ignoring re-rolls.</summary>
    public double Probability
    {
        get
        {
            if (FirstRoll is not { } first) return 0.0;
            double firstChance = (7 - first) / 6.0;
            if (SecondRoll is not { } second) return firstChance;
            return firstChance * ((7 - second) / 6.0);
        }
    }

    /// <summary>
    /// Roll against this requirement. A natural 1 always fails and a natural 6 on a single-roll
    /// requirement always succeeds; a compound requirement needs its 6 and then a confirming D6.
    /// </summary>
    /// <returns>
    /// Whether it succeeded, and the standing value of the <em>first</em> die — which is what
    /// natural-6 effects such as Killing Blow key on. Callers re-roll by calling again, so those
    /// effects see the roll that stands rather than the one originally thrown.
    /// </returns>
    public (bool Success, int Roll) Roll(Random rng)
    {
        if (FirstRoll is not { } needed) return (false, 0);

        int roll = rng.Next(1, 7);

        if (SecondRoll is { } confirm)
        {
            // The chart demands the first die before a confirming roll is even made.
            if (roll < needed) return (false, roll);
            int second = rng.Next(1, 7);
            return (second != 1 && second >= confirm, roll);
        }

        return (roll == 6 || (roll != 1 && roll >= needed), roll);
    }

    /// <summary>Parses a chart cell: "4+", "6+/3+" or "n/a". Bare "4" is accepted too.</summary>
    public static bool TryParse(string? cell, out RollRequirement result)
    {
        result = Impossible;
        var text = cell?.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        if (text.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || text.Equals("na", StringComparison.OrdinalIgnoreCase)
            || text == "-")
        {
            result = Impossible;
            return true;
        }

        var parts = text.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length > 2) return false;

        if (!TryParseScore(parts[0], out int firstScore)) return false;

        if (parts.Length == 1)
        {
            result = Plain(firstScore);
            return true;
        }

        if (!TryParseScore(parts[1], out int secondScore)) return false;

        // Only a 6 can carry a confirming roll; anything else is malformed data.
        if (firstScore != 6) return false;

        result = Compound(secondScore);
        return true;
    }

    private static bool TryParseScore(string text, out int score)
    {
        score = 0;
        var trimmed = text.TrimEnd('+').Trim();
        return int.TryParse(trimmed, out score) && score is >= 1 and <= 6;
    }

    public override string ToString()
    {
        if (FirstRoll is not { } first) return "n/a";
        return SecondRoll is { } second ? $"{first}+/{second}+" : $"{first}+";
    }
}
