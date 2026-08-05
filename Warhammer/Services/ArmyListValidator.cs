using System.Text.RegularExpressions;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Services;

public class ArmyListValidator
{
    private static readonly Regex PerArmyPattern =
        new(@"\b0-(\d+)\s+per\s+army\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <param name="rules">
    /// The active system's percentages. Passed per call rather than held as state because this
    /// is a singleton and the selected system can change at runtime. Any
    /// <see cref="ArmyList.Overrides"/> on the list win over these.
    /// </param>
    public List<ValidationIssue> Validate(ArmyList list, CompositionRules rules)
    {
        var issues = new List<ValidationIssue>();

        if (list.PointsTarget <= 0)
        {
            issues.Add(new(ValidationSeverity.Error, "INVALID_TARGET",
                $"Points target must be positive (was {list.PointsTarget})"));
            return issues;
        }

        var limits = rules.Resolve(list.Overrides, list.PointsTarget);

        // Null limit = neither the system nor the list imposes one
        // (wap-v3 has no Lords/Heroes sections).
        if (limits.LordsMax is { } lordsMax)
            CheckMax(issues, list, UnitCategory.Lords, lordsMax, "LORDS_OVER_CAP", "Lords");

        if (limits.HeroesMax is { } heroesMax)
            CheckMax(issues, list, UnitCategory.Heroes, heroesMax, "HEROES_OVER_CAP", "Heroes");

        double charactersPoints =
            list.PointsInCategory(UnitCategory.Lords) +
            list.PointsInCategory(UnitCategory.Heroes) +
            list.PointsInCategory(UnitCategory.Characters);
        if (charactersPoints > limits.CharactersCombinedMax.Points)
        {
            issues.Add(new(ValidationSeverity.Error, "CHARACTERS_OVER_CAP",
                $"Characters total {charactersPoints:F0} exceeds combined cap " +
                $"{limits.CharactersCombinedMax.Points:F0} ({limits.CharactersCombinedMax.Description})"));
        }

        double corePoints = list.PointsInCategory(UnitCategory.Core);
        if (corePoints < limits.CoreMin.Points)
        {
            issues.Add(new(ValidationSeverity.Error, "CORE_BELOW_MIN",
                $"Core total {corePoints:F0} is below minimum {limits.CoreMin.Points:F0} " +
                $"({limits.CoreMin.Description})"));
        }

        CheckMax(issues, list, UnitCategory.Special, limits.SpecialMax, "SPECIAL_OVER_CAP", "Special");
        CheckMax(issues, list, UnitCategory.Rare, limits.RareMax, "RARE_OVER_CAP", "Rare");

        var characters = list.Units
            .Where(u => u.ResolvedUnit.Category.IsCharacterCategory())
            .ToList();

        if (limits.SingleCharacterMax is { } singleMax)
        {
            foreach (var character in characters.Where(c => c.TotalPointsCost > singleMax.Points))
            {
                issues.Add(new(ValidationSeverity.Error, "SINGLE_CHARACTER_OVER_CAP",
                    $"{character.ResolvedUnit.Name} costs {character.TotalPointsCost:F0}, " +
                    $"over the {singleMax.Points:F0} limit for one character " +
                    $"({singleMax.Description})",
                    character));
            }
        }

        if (characters.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Error, "NO_GENERAL",
                "Army must include at least one character to act as General"));
        }
        else
        {
            var generals = characters.Where(c => c.Loadout.IsGeneral).ToList();
            if (generals.Count == 0)
            {
                issues.Add(new(ValidationSeverity.Warning, "NO_GENERAL_DESIGNATED",
                    "No General designated — pick one character to lead the army"));
            }
            else if (generals.Count > 1)
            {
                issues.Add(new(ValidationSeverity.Error, "MULTIPLE_GENERALS",
                    $"Exactly one character may be General (found {generals.Count})"));
            }
        }

        var bsbs = list.Units.Where(u => u.Loadout.IsBattleStandardBearer).ToList();
        if (bsbs.Count > 1)
        {
            issues.Add(new(ValidationSeverity.Error, "MULTIPLE_BSB",
                $"Only one character may carry the Battle Standard (found {bsbs.Count})"));
        }
        foreach (var bsb in bsbs)
        {
            if (!bsb.IsBattleStandardEligible)
            {
                issues.Add(new(ValidationSeverity.Error, "INVALID_BSB",
                    $"{bsb.ResolvedUnit.Name} cannot carry the Battle Standard",
                    bsb));
            }
        }

        if (list.TotalPoints > list.PointsTarget)
        {
            issues.Add(new(ValidationSeverity.Error, "OVER_POINTS_TARGET",
                $"Total {list.TotalPoints:F0} exceeds target {list.PointsTarget}"));
        }

        foreach (var group in list.Units.GroupBy(u => u.ResolvedUnit.Name))
        {
            var representative = group.First();
            var notes = representative.UnitEntry.Notes ?? "";
            var match = PerArmyPattern.Match(notes);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var cap) && group.Count() > cap)
            {
                issues.Add(new(ValidationSeverity.Error, "PER_ARMY_LIMIT_EXCEEDED",
                    $"{group.Key}: 0-{cap} per army limit exceeded ({group.Count()} in list)",
                    representative));
            }
        }

        int specialCap = limits.SpecialDuplicateLimit;
        int rareCap = limits.RareDuplicateLimit;
        foreach (var group in list.Units.GroupBy(u => u.ResolvedUnit.Name))
        {
            var representative = group.First();
            var category = representative.ResolvedUnit.Category;
            int count = group.Count();

            if (category == UnitCategory.Special && count > specialCap)
            {
                issues.Add(new(ValidationSeverity.Error, "DUPLICATE_LIMIT_EXCEEDED",
                    $"{group.Key}: Special duplicate limit {specialCap} exceeded ({count} in list)",
                    representative));
            }
            else if (category == UnitCategory.Rare && count > rareCap)
            {
                issues.Add(new(ValidationSeverity.Error, "DUPLICATE_LIMIT_EXCEEDED",
                    $"{group.Key}: Rare duplicate limit {rareCap} exceeded ({count} in list)",
                    representative));
            }
        }

        return issues;
    }

    private static void CheckMax(
        List<ValidationIssue> issues,
        ArmyList list,
        UnitCategory category,
        EffectiveLimit limit,
        string code,
        string label)
    {
        double points = list.PointsInCategory(category);
        if (points > limit.Points)
        {
            issues.Add(new(ValidationSeverity.Error, code,
                $"{label} total {points:F0} exceeds {limit.Points:F0} ({limit.Description})"));
        }
    }
}
