using System.Text.RegularExpressions;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Services;

public class ArmyListValidator
{
    private static readonly Regex PerArmyPattern =
        new(@"\b0-(\d+)\s+per\s+army\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public List<ValidationIssue> Validate(ArmyList list)
    {
        var issues = new List<ValidationIssue>();

        if (list.PointsTarget <= 0)
        {
            issues.Add(new(ValidationSeverity.Error, "INVALID_TARGET",
                $"Points target must be positive (was {list.PointsTarget})"));
            return issues;
        }

        CheckMaxPercent(issues, list, UnitCategory.Lords,
            ArmyCompositionRules.LordsMaxPercent, "LORDS_OVER_CAP", "Lords");

        CheckMaxPercent(issues, list, UnitCategory.Heroes,
            ArmyCompositionRules.HeroesMaxPercent, "HEROES_OVER_CAP", "Heroes");

        double charactersPoints =
            list.PointsInCategory(UnitCategory.Lords) +
            list.PointsInCategory(UnitCategory.Heroes);
        double charactersCap = list.PointsTarget * ArmyCompositionRules.CharactersCombinedMaxPercent;
        if (charactersPoints > charactersCap)
        {
            issues.Add(new(ValidationSeverity.Error, "CHARACTERS_OVER_CAP",
                $"Characters total {charactersPoints:F0} exceeds combined cap {charactersCap:F0} " +
                $"({ArmyCompositionRules.CharactersCombinedMaxPercent:P0} of {list.PointsTarget})"));
        }

        double corePoints = list.PointsInCategory(UnitCategory.Core);
        double coreMin = list.PointsTarget * ArmyCompositionRules.CoreMinPercent;
        if (corePoints < coreMin)
        {
            issues.Add(new(ValidationSeverity.Error, "CORE_BELOW_MIN",
                $"Core total {corePoints:F0} is below minimum {coreMin:F0} " +
                $"({ArmyCompositionRules.CoreMinPercent:P0} of {list.PointsTarget})"));
        }

        CheckMaxPercent(issues, list, UnitCategory.Special,
            ArmyCompositionRules.SpecialMaxPercent, "SPECIAL_OVER_CAP", "Special");

        CheckMaxPercent(issues, list, UnitCategory.Rare,
            ArmyCompositionRules.RareMaxPercent, "RARE_OVER_CAP", "Rare");

        var characters = list.Units
            .Where(u => u.ResolvedUnit.Category is UnitCategory.Lords or UnitCategory.Heroes)
            .ToList();
        if (characters.Count == 0)
        {
            issues.Add(new(ValidationSeverity.Error, "NO_GENERAL",
                "Army must include at least one Lord or Hero to act as General"));
        }
        else
        {
            var generals = characters.Where(c => c.IsGeneral).ToList();
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

        var bsbs = list.Units.Where(u => u.IsBattleStandardBearer).ToList();
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

        var (specialCap, rareCap) = ArmyCompositionRules.DuplicateLimits(list.PointsTarget);
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

    private static void CheckMaxPercent(
        List<ValidationIssue> issues,
        ArmyList list,
        UnitCategory category,
        double maxPercent,
        string code,
        string label)
    {
        double points = list.PointsInCategory(category);
        double cap = list.PointsTarget * maxPercent;
        if (points > cap)
        {
            issues.Add(new(ValidationSeverity.Error, code,
                $"{label} total {points:F0} exceeds {cap:F0} ({maxPercent:P0} of {list.PointsTarget})"));
        }
    }
}
