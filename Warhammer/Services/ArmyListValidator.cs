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

        double charactersPoints = UnitCategoryExtensions.CharacterCategories
            .Sum(list.PointsInCategory);
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

        // "No single character or unit in your army may cost more than 25% of your total
        // points" — the cap covers every entry, not just characters.
        if (limits.SingleUnitMax is { } singleMax)
        {
            foreach (var unit in list.Units.Where(u => u.TotalPointsCost > singleMax.Points))
            {
                issues.Add(new(ValidationSeverity.Error, "SINGLE_UNIT_OVER_CAP",
                    $"{unit.ResolvedUnit.Name} costs {unit.TotalPointsCost:F0}, " +
                    $"over the {singleMax.Points:F0} limit for one unit " +
                    $"({singleMax.Description})",
                    unit));
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

        CheckMagicItems(issues, list);

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

    /// <summary>
    /// Magic item rules: an entry may spend up to the allowance its army book prints, on magic
    /// items and on the upgrades the same line offers ("Quirks of Character and/or Magic Items
    /// up to a total of 100 points"), and no model may hold two items from one of the six
    /// printed categories. Dwarf runes are exempt from that limit — up to three go on a single
    /// item — but no master rune may be repeated across the army. Other duplicates are only a
    /// warning: the books exempt some items, and the extracted item lists don't record which.
    /// </summary>
    private static void CheckMagicItems(List<ValidationIssue> issues, ArmyList list)
    {
        foreach (var unit in list.Units)
        {
            string name = unit.DisplayName;
            int? allowance = unit.ResolvedUnit.MagicItemAllowance;

            if (unit.SelectedMagicItems.Count > 0 && allowance is null)
            {
                issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEMS_NOT_ALLOWED",
                    $"{name} has no magic item allowance but carries " +
                    $"{unit.SelectedMagicItems.Count} item(s)",
                    unit));
            }
            else if (allowance is int cap && unit.MagicItemAllowanceSpent > cap)
            {
                var spentOn = unit.AllowanceUpgradeCategories.Select(c => c.Replace('_', ' ')).ToList();
                if (unit.SelectedMagicItems.Count > 0)
                    spentOn.Insert(0, "magic items");
                issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEMS_OVER_BUDGET",
                    $"{name} spends {unit.MagicItemAllowanceSpent:F0} on {string.Join(" and ", spentOn)}, " +
                    $"over its {cap} allowance",
                    unit));
            }

            foreach (var category in unit.SelectedMagicItems
                         .Where(i => MagicItemCategories.HasOnePerModelLimit(i.Category))
                         .GroupBy(i => i.Category)
                         .Where(g => g.Count() > 1))
            {
                issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEM_CATEGORY_REPEATED",
                    $"{name} carries {category.Count()} items from " +
                    $"{MagicItemCategories.Plural(category.Key)} — a model may only have one " +
                    $"{MagicItemCategories.Singular(category.Key)} " +
                    $"({string.Join(", ", category.Select(i => i.Name))})",
                    unit));
            }

            if (unit.Loadout.SelectedBanner is { } banner)
            {
                if (unit.BannerBudget is not int bannerCap)
                {
                    issues.Add(new(ValidationSeverity.Error, "BANNER_NOT_ALLOWED",
                        $"{name} carries {banner.Name} but cannot take a magic standard",
                        unit));
                }
                else if (banner.PointsCost > bannerCap)
                {
                    issues.Add(new(ValidationSeverity.Error, "BANNER_OVER_BUDGET",
                        $"{name}: {banner.Name} costs {banner.PointsCost:F0}, " +
                        $"over the {bannerCap} magic standard allowance",
                        unit));
                }
            }
        }

        var carried = list.Units.SelectMany(CarriedItems);

        foreach (var group in carried.GroupBy(c => c.ItemName, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            string repeated = $"{group.Key} is carried {group.Count()} times " +
                              $"({string.Join(", ", group.Select(c => c.Unit.DisplayName))})";

            if (MagicItemCategories.IsMasterRune(group.Key))
            {
                issues.Add(new(ValidationSeverity.Error, "MASTER_RUNE_REPEATED",
                    $"{repeated} — no master rune may be used more than once per army",
                    group.First().Unit));
            }
            else
            {
                issues.Add(new(ValidationSeverity.Warning, "DUPLICATE_MAGIC_ITEM",
                    $"{repeated} — most magic items may only be taken once per army",
                    group.First().Unit));
            }
        }
    }

    /// <summary>Every magic item on a unit: its own picks, plus the magic standard it carries.</summary>
    private static IEnumerable<(UnitConfiguration Unit, string ItemName)> CarriedItems(UnitConfiguration unit)
    {
        foreach (var item in unit.SelectedMagicItems)
            yield return (unit, item.Name);

        if (unit.Loadout.SelectedBanner is { } banner)
            yield return (unit, banner.Name);
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
