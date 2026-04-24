namespace Warhammer.Models.Domain;

public static class ArmyCompositionRules
{
    public const double LordsMaxPercent = 0.25;
    public const double HeroesMaxPercent = 0.25;
    public const double CharactersCombinedMaxPercent = 0.35;
    public const double CoreMinPercent = 0.25;
    public const double SpecialMaxPercent = 0.50;
    public const double RareMaxPercent = 0.25;

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
