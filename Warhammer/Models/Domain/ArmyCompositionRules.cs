namespace Warhammer.Models.Domain;

/// <summary>
/// Composition limits that are a rule shape rather than a percentage. The percentages
/// themselves are per-system data — see <see cref="CompositionRules"/>.
/// </summary>
public static class ArmyCompositionRules
{
    // Duplicates per Special/Rare unit type, scaling with points target.
    // 0-999 → 1/1, 1000-1999 → 2/1, 2000-2999 → 3/1, 3000-3999 → 4/2, 4000-4999 → 5/3,
    // then +1 to each per additional +1000 points.
    public static (int SpecialDupes, int RareDupes) DuplicateLimits(int pointsTarget)
    {
        int bracket = Math.Max(0, pointsTarget) / 1000;
        int special = bracket + 1;
        int rare = 1 + Math.Max(0, bracket - 2);
        return (special, rare);
    }
}
