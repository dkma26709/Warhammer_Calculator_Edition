using System.Globalization;
using System.Text.RegularExpressions;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Models.Domain;

/// <summary>The sections of a unit's options editor, in display order.</summary>
public enum OptionSection { Weapons, Armour, Equipment, Mount, Upgrades }

/// <summary>What one piece of gear named in an option is, as far as the name tells.</summary>
public enum GearKind { CloseCombatWeapon, MissileWeapon, Armour, Unknown }

/// <summary>One option the editor shows: where it came from, which section it sits in, and its heading.</summary>
public sealed record OptionPlacement(int Index, UnitOption Option, OptionSection Section, string Label);

/// <summary>
/// Decides which section of the options editor every unit option belongs in and what its
/// control is headed, so the Builder and the calculator sort a unit's gear the same way and
/// the rule can be tested without rendering a component.
/// </summary>
public static class OptionLayout
{
    /// <summary>
    /// Armour the core table does not name — "Gut-plates", "Fur cloaks", "Natural armour (6+)".
    /// Whole words only, so a "Shieldbearers" upgrade is not taken for a shield.
    /// </summary>
    private static readonly Regex ArmourWord = new(
        @"\b(armou?r|shields?|bucklers?|barding|cloaks?|plates?|mantlets?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Cost text at the end of an upgrade's name — the " 5 points" of "Look-out Gnoblar 5 points".</summary>
    private static readonly Regex TrailingCost = new(
        @"\s*\+?\d+(\.\d+)?\s*(points?|pts)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The first "N points" or "N pts" anywhere in a line; group 1 is the number.</summary>
    private static readonly Regex CostInText = new(
        @"(\d+(\.\d+)?)\s*(points?|pts)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The kind of one item name. A name the core tables know is classified by its key; any
    /// other is armour if it says so and unknown otherwise, which leaves faction gear such as a
    /// "Sunstaff" to take the colour of the items it is offered beside.
    /// </summary>
    public static GearKind KindOf(string itemName)
    {
        if (EquipmentParser.TryNormalize(itemName, out var key, out bool isWeapon))
        {
            if (!isWeapon) return GearKind.Armour;
            return CloseCombatWeaponKeyExtensions.TryFromJsonKey(key, out _)
                ? GearKind.CloseCombatWeapon
                : GearKind.MissileWeapon;
        }

        return ArmourWord.IsMatch(itemName) ? GearKind.Armour : GearKind.Unknown;
    }

    /// <summary>
    /// The section an option is shown in, or null when the options editor does not show it
    /// (command, magic items and banners have panels of their own). A flat upgrade that names a
    /// weapon or armour — "Blowpipe", "Full Plate" — sits with that gear rather than with the
    /// unit's other upgrades.
    /// </summary>
    public static OptionSection? SectionOf(UnitOption option) => option.GetOptionType() switch
    {
        OptionType.Equipment => EquipmentSection(option),
        OptionType.Mount => OptionSection.Mount,
        OptionType.Upgrade when option.SubOptions != null => OptionSection.Upgrades,
        OptionType.Upgrade => KindOf(option.Upgrade ?? "") switch
        {
            GearKind.CloseCombatWeapon or GearKind.MissileWeapon => OptionSection.Weapons,
            GearKind.Armour => OptionSection.Armour,
            _ => OptionSection.Upgrades,
        },
        OptionType.WizardUpgrade or OptionType.BlessedSpawning or OptionType.FactionUpgrade
            => OptionSection.Upgrades,
        _ => null,
    };

    /// <summary>
    /// The heading of an option's control. A single choice is a checkbox, so it is headed by
    /// the item itself; a dropdown or a group of checkboxes is headed by what it chooses
    /// between, and says when more than one may be taken.
    /// </summary>
    public static string LabelFor(UnitOption option) => option.GetOptionType() switch
    {
        OptionType.Equipment => EquipmentLabel(option),
        OptionType.Mount => "Mount",
        OptionType.Upgrade when option.SubOptions != null =>
            string.IsNullOrWhiteSpace(option.Upgrade)
            || option.Upgrade.StartsWith("May ", StringComparison.OrdinalIgnoreCase)
                ? "Upgrade"
                : option.Upgrade,
        _ => option.Upgrade ?? option.Type,
    };

    /// <summary>
    /// A choice's name as a control shows it, capitalised because WAR writes single items in
    /// lower case ("shield", "javelins"), and without any cost the extractor left in it
    /// ("brace of Ogre Pistols 6 points" is "Brace of Ogre Pistols").
    /// </summary>
    public static string ChoiceName(OptionChoice choice)
    {
        var name = TrailingCost.Replace(choice.DisplayName, "").Trim();
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// A choice's cost: its own points, else the cost written into its name, else — when it is
    /// the group's only choice — the cost in the group's book line, else the group's points.
    /// </summary>
    public static double ChoicePoints(OptionChoice choice, UnitOption group) =>
        choice.Points
        ?? CostIn(choice.DisplayName)
        ?? (group.Choices is { Count: 1 } ? CostIn(group.Raw) : null)
        ?? group.Points
        ?? 0;

    /// <summary>A cost as a control shows it: "(free)", "(+5 pts)" or "(+0.5 pts/model)".</summary>
    public static string CostLabel(double points, bool perModel)
    {
        if (points == 0) return "(free)";
        var pts = points % 1 == 0 ? points.ToString("F0") : points.ToString("G");
        return perModel ? $"(+{pts} pts/model)" : $"(+{pts} pts)";
    }

    /// <summary>A choice's cost as a control shows it; a choice the book gave no price is free.</summary>
    public static string CostLabel(OptionChoice choice) => CostLabel(choice.Points ?? 0, choice.PerModel);

    /// <summary>
    /// An upgrade's name without the cost the book wrote into it ("Look-out Gnoblar 5 points"
    /// is "Look-out Gnoblar"), or "Upgrade" when that leaves nothing.
    /// </summary>
    public static string UpgradeName(UnitOption subOption)
    {
        var name = TrailingCost.Replace(subOption.Upgrade ?? "", "").Trim();
        return name.Length == 0 ? "Upgrade" : name;
    }

    /// <summary>
    /// An upgrade's cost: its own points, else the first "N points" in its name, then in its
    /// book line, else nothing — the extractor leaves the cost in the text when it misses it.
    /// </summary>
    public static double UpgradePoints(UnitOption subOption) =>
        subOption.Points ?? CostIn(subOption.Upgrade) ?? CostIn(subOption.Raw) ?? 0;

    /// <summary>The first cost written in <paramref name="text"/>, or null when it names none.</summary>
    private static double? CostIn(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var match = CostInText.Match(text);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// True for an equipment group that offers nothing but close combat weapons — the groups
    /// the calculator folds into its "Fight with" dropdown. One choice that also brings a
    /// shield or a bow ("Light lances &amp; shields") keeps the group its own control.
    /// </summary>
    public static bool IsCloseCombatWeaponGroup(UnitOption option) =>
        option.IsType(OptionType.Equipment)
        && option.Choices is { Count: > 0 } choices
        && choices.All(c => c.Name.Count > 0
            && c.Name.All(n => KindOf(n) == GearKind.CloseCombatWeapon));

    /// <summary>
    /// Every option the editor shows, grouped by section in display order and in book order
    /// within a section. <see cref="OptionPlacement.Index"/> stays the index into the unit's
    /// own list, which is what the selection dictionaries are keyed by.
    /// </summary>
    public static IReadOnlyList<OptionPlacement> Arrange(IReadOnlyList<UnitOption>? options)
    {
        if (options is null || options.Count == 0) return [];

        return options
            .Select((option, index) => (Option: option, Index: index, Section: SectionOf(option)))
            .Where(o => o.Section != null)
            .OrderBy(o => o.Section!.Value)
            .ThenBy(o => o.Index)
            .Select(o => new OptionPlacement(o.Index, o.Option, o.Section!.Value, LabelFor(o.Option)))
            .ToList();
    }

    /// <summary>The kinds of every item an option offers, less the unknown ones, which do not decide the section.</summary>
    private static HashSet<GearKind> KnownKinds(UnitOption option) =>
        (option.Choices ?? [])
            .SelectMany(c => c.Name)
            .Select(KindOf)
            .Where(k => k != GearKind.Unknown)
            .ToHashSet();

    /// <summary>Weapons or armour when every known item is one of them; equipment when mixed or all unknown.</summary>
    private static OptionSection? EquipmentSection(UnitOption option)
    {
        if (option.Choices is not { Count: > 0 }) return null;

        var kinds = KnownKinds(option);
        if (kinds.Count > 0 && kinds.All(k => k is GearKind.CloseCombatWeapon or GearKind.MissileWeapon))
            return OptionSection.Weapons;
        if (kinds.Count == 1 && kinds.Contains(GearKind.Armour))
            return OptionSection.Armour;
        return OptionSection.Equipment;
    }

    /// <summary>A checkbox is headed by its choice; a group by what it replaces or its section, and how many it takes.</summary>
    private static string EquipmentLabel(UnitOption option)
    {
        var choices = option.Choices ?? [];
        bool replace = option.IsAction(OptionAction.Replace);

        if (choices.Count == 1)
        {
            var choice = choices[0];
            var replaced = choice.Replaces ?? option.Replaces;
            return replace && !string.IsNullOrWhiteSpace(replaced)
                ? $"{ChoiceName(choice)} (replaces {replaced})"
                : ChoiceName(choice);
        }

        var label = replace && !string.IsNullOrWhiteSpace(option.Replaces)
            ? $"Replace {option.Replaces} with"
            : EquipmentSection(option) switch
            {
                OptionSection.Weapons => WeaponLabel(KnownKinds(option)),
                OptionSection.Armour => "Armour",
                _ => "Equipment",
            };

        if (!option.AllowsMultipleChoices) return label;

        return option.Raw!.Contains("at least one", StringComparison.OrdinalIgnoreCase)
            ? $"{label} (at least one)"
            : $"{label} (any)";
    }

    /// <summary>Names a weapons group by the kind of weapon it offers.</summary>
    private static string WeaponLabel(HashSet<GearKind> kinds) =>
        kinds.Contains(GearKind.CloseCombatWeapon) && kinds.Contains(GearKind.MissileWeapon) ? "Weapon"
        : kinds.Contains(GearKind.CloseCombatWeapon) ? "Close combat weapon"
        : "Missile weapon";
}
