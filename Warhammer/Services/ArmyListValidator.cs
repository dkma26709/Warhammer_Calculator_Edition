using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class ArmyListValidator
{
    /// <param name="rules">
    /// The active system's rules, with the faction's own unit constraints. Passed per call
    /// rather than held as state because this is a singleton and the selected faction can
    /// change at runtime. Any <see cref="ArmyList.Overrides"/> on the list win over these.
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
        // (a book with one characters section has no Lords/Heroes caps).
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

        if (rules.GeneralRequired)
        {
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
        }

        var bsbs = list.Units.Where(u => u.Loadout.IsBattleStandardBearer).ToList();
        if (bsbs.Count > rules.BattleStandardMax)
        {
            issues.Add(new(ValidationSeverity.Error, "MULTIPLE_BSB",
                $"Only {rules.BattleStandardMax} character may carry the Battle Standard (found {bsbs.Count})"));
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
        CheckFactionUpgrades(issues, list);

        // "Each special character may be included once."
        foreach (var group in list.Units.Where(u => u.ResolvedUnit.Named).GroupBy(u => u.ResolvedUnit.Id))
        {
            if (group.Count() > rules.SpecialCharactersMaxEach)
            {
                issues.Add(new(ValidationSeverity.Error, "SPECIAL_CHARACTER_REPEATED",
                    $"{group.First().ResolvedUnit.Name} is a special character and may be included " +
                    $"{rules.SpecialCharactersMaxEach} time(s) ({group.Count()} in list)",
                    group.First()));
            }
        }

        CheckUnitConstraints(issues, list, rules);
        CheckDuplicates(issues, list, rules, limits);

        return issues;
    }

    /// <summary>
    /// Magic item rules: an entry may spend up to the allowance its army book prints — the
    /// picks from any upgrade family the same allowance pays for included — only from the
    /// lists it admits, and no model may hold two items from one of the six printed lists.
    /// Other duplicates are only a warning: the books exempt some items, and the item lists
    /// don't record which.
    /// </summary>
    private static void CheckMagicItems(List<ValidationIssue> issues, ArmyList list)
    {
        foreach (var unit in list.Units)
        {
            string name = unit.DisplayName;
            var allowance = unit.MagicItemAllowance;

            if (unit.SelectedMagicItems.Count > 0)
            {
                if (allowance is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEMS_NOT_ALLOWED",
                        $"{name} has no magic item allowance but carries " +
                        $"{unit.SelectedMagicItems.Count} item(s)",
                        unit));
                }
                else
                {
                    foreach (var item in unit.SelectedMagicItems.Where(i => !allowance.Admits(i.Category)))
                    {
                        issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEM_TYPE_NOT_ALLOWED",
                            $"{name} may only take {string.Join(", ", allowance.ItemTypes!.Select(MagicItemCategories.Plural))}, " +
                            $"not {item.Name} ({MagicItemCategories.Singular(item.Category)})",
                            unit));
                    }
                }

                foreach (var category in unit.SelectedMagicItems
                             .Where(i => MagicItemCategories.HasOnePerModelLimit(i.Category))
                             .GroupBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
                             .Where(g => g.Count() > 1))
                {
                    issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEM_CATEGORY_REPEATED",
                        $"{name} carries {category.Count()} items from " +
                        $"{MagicItemCategories.Plural(category.Key)} — a model may only have one " +
                        $"{MagicItemCategories.Singular(category.Key)} " +
                        $"({string.Join(", ", category.Select(i => i.Name))})",
                        unit));
                }
            }

            if (allowance?.PointsLimit is { } limit)
            {
                double spent = unit.MagicItemSpend;
                if (spent > limit)
                {
                    issues.Add(new(ValidationSeverity.Error, "MAGIC_ITEMS_OVER_BUDGET",
                        $"{name} spends {spent:F0} on magic items, over its {limit} allowance",
                        unit));
                }
            }

            if (unit.Loadout.SelectedBanner is { } banner)
            {
                if (unit.MagicStandardAllowance is not { } bannerAllowance)
                {
                    issues.Add(new(ValidationSeverity.Error, "BANNER_NOT_ALLOWED",
                        $"{name} carries {banner.Name} but cannot take a magic standard",
                        unit));
                }
                else if (bannerAllowance.PointsLimit is { } bannerCap && banner.PointsCost > bannerCap)
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

    /// <summary>
    /// Faction upgrade rules: a family the book caps — "engineering runes up to 50 points" —
    /// may not be overspent, and a family an entry is not offered cannot be picked from.
    /// </summary>
    private static void CheckFactionUpgrades(List<ValidationIssue> issues, ArmyList list)
    {
        foreach (var unit in list.Units)
        {
            var offered = unit.OfferedFamilies.ToDictionary(f => f.Family, StringComparer.OrdinalIgnoreCase);

            foreach (var (family, picks) in unit.Loadout.SelectedFactionUpgrades)
            {
                if (picks.Count == 0) continue;

                if (!offered.TryGetValue(family, out var offer))
                {
                    issues.Add(new(ValidationSeverity.Error, "FACTION_UPGRADE_NOT_ALLOWED",
                        $"{unit.DisplayName} is not offered {family.Replace('-', ' ')} but takes " +
                        $"{string.Join(", ", picks.Select(p => p.Name))}",
                        unit));
                    continue;
                }

                // A family paid for by the magic item allowance is checked with the items.
                if (offer.ThroughMagicItems || offer.PointsLimit is not { } cap) continue;

                double spent = picks.Sum(p => p.PointsCost);
                if (spent > cap)
                {
                    issues.Add(new(ValidationSeverity.Error, "FACTION_UPGRADE_OVER_BUDGET",
                        $"{unit.DisplayName} spends {spent:F0} on {family.Replace('-', ' ')}, over its {cap} allowance",
                        unit));
                }
            }
        }
    }

    /// <summary>
    /// The constraints a book prints under its units, as the bundle reads them. Handler ratios
    /// within a unit are not checked: the handler's count is not something the list records.
    /// </summary>
    private static void CheckUnitConstraints(List<ValidationIssue> issues, ArmyList list, CompositionRules rules)
    {
        foreach (var constraint in rules.UnitConstraints)
        {
            var affected = list.Units
                .Where(u => constraint.UnitIds.Contains(u.ResolvedUnit.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (affected.Count == 0) continue;

            var representative = affected[0];
            string label = representative.ResolvedUnit.Name;

            switch (constraint.Type)
            {
                case "max":
                case "limited":
                {
                    var counted = constraint.ProfileId is { } profile
                        ? affected.Where(u => string.Equals(u.SelectedProfileId, profile, StringComparison.OrdinalIgnoreCase)).ToList()
                        : affected;
                    if (constraint.Max is not { } max) break;

                    int cap = constraint.PerPoints is { } per && per > 0
                        ? max * Math.Max(1, list.PointsTarget / per)
                        : max;

                    if (counted.Count > cap)
                    {
                        issues.Add(new(ValidationSeverity.Error, "PER_ARMY_LIMIT_EXCEEDED",
                            $"{label}: {constraint.Raw} ({counted.Count} in list, limit {cap})",
                            counted[0]));
                    }
                    break;
                }

                case "ratio":
                {
                    int have = list.Units.Count(u => Satisfies(u, constraint));
                    int max = constraint.Max ?? 1;
                    int allowed = constraint.PerUnit == true ? have * max : max;
                    if (affected.Count > allowed)
                    {
                        issues.Add(new(ValidationSeverity.Error, "RATIO_EXCEEDED",
                            $"{label}: {constraint.Raw} ({affected.Count} in list, {have} required unit(s))",
                            representative));
                    }
                    break;
                }

                case "general":
                {
                    if (constraint.MayBeGeneral == false)
                    {
                        foreach (var unit in affected.Where(u => u.Loadout.IsGeneral))
                        {
                            issues.Add(new(ValidationSeverity.Error, "MAY_NOT_BE_GENERAL",
                                $"{unit.DisplayName}: {constraint.Raw}", unit));
                        }
                    }

                    if (constraint.MustBeGeneral == true
                        && !affected.Any(u => u.Loadout.IsGeneral)
                        && list.Units.Any(u => u.Loadout.IsGeneral))
                    {
                        issues.Add(new(ValidationSeverity.Error, "MUST_BE_GENERAL",
                            $"{label}: {constraint.Raw}", representative));
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Whether a unit is one of those a ratio constraint counts against — by id, or by a rule it has.</summary>
    private static bool Satisfies(UnitConfiguration unit, UnitConstraintEntry constraint)
    {
        var id = unit.ResolvedUnit.Id;
        if (constraint.Requires?.Contains(id, StringComparer.OrdinalIgnoreCase) == true) return true;
        if (constraint.RequiresRule is { } rule
            && unit.SpecialRules.AllRuleNames.Any(r => r.Contains(rule, StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    /// <summary>
    /// The duplicate chart: how many of one Special or Rare entry a list may field at its
    /// points target. A book may let two of an entry fill one choice — "1-2 Bolt Throwers as a
    /// single Rare choice" — in which case the pair counts once.
    /// </summary>
    private static void CheckDuplicates(List<ValidationIssue> issues, ArmyList list, CompositionRules rules, EffectiveLimits limits)
    {
        int specialCap = limits.SpecialDuplicateLimit;
        int rareCap = limits.RareDuplicateLimit;

        foreach (var group in list.Units.GroupBy(u => u.ResolvedUnit.Id))
        {
            var representative = group.First();
            var category = representative.ResolvedUnit.Category;
            int count = group.Count();

            var perSlot = rules.UnitConstraints.FirstOrDefault(c =>
                c.Type == "perSlot"
                && c.PerSlot is { Max: > 0 }
                && c.UnitIds.Contains(group.Key, StringComparer.OrdinalIgnoreCase));
            int choices = perSlot is not null
                ? (count + perSlot.PerSlot!.Max - 1) / perSlot.PerSlot.Max
                : count;

            string name = representative.ResolvedUnit.Name;
            if (category == UnitCategory.Special && choices > specialCap)
            {
                issues.Add(new(ValidationSeverity.Error, "DUPLICATE_LIMIT_EXCEEDED",
                    $"{name}: Special duplicate limit {specialCap} exceeded ({count} in list)",
                    representative));
            }
            else if (category == UnitCategory.Rare && choices > rareCap)
            {
                issues.Add(new(ValidationSeverity.Error, "DUPLICATE_LIMIT_EXCEEDED",
                    $"{name}: Rare duplicate limit {rareCap} exceeded ({count} in list)",
                    representative));
            }
        }
    }

    /// <summary>
    /// Every magic item on a unit: its own picks, the magic standard it carries, and the runes
    /// and other upgrade-family picks that answer to the once-per-army rule.
    /// </summary>
    private static IEnumerable<(UnitConfiguration Unit, string ItemName)> CarriedItems(UnitConfiguration unit)
    {
        foreach (var item in unit.SelectedMagicItems)
            yield return (unit, item.Name);

        if (unit.Loadout.SelectedBanner is { } banner)
            yield return (unit, banner.Name);

        foreach (var picks in unit.Loadout.SelectedFactionUpgrades.Values)
            foreach (var pick in picks.Where(p => MagicItemCategories.IsMasterRune(p.Name)))
                yield return (unit, pick.Name);
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
