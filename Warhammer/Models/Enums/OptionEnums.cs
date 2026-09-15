using Warhammer.Models.Json;

namespace Warhammer.Models.Enums;

/// <summary>What kind of thing an option group offers — the bundle's <c>kind</c>.</summary>
public enum OptionKind
{
    Equipment,
    Mount,
    Command,
    MagicStandard,
    MagicItems,
    BattleStandard,
    WizardLevel,
    FactionUpgrade,
    SpecialRule,
    Upgrade,
}

/// <summary>How many of a group's choices may be taken — the bundle's <c>select</c>.</summary>
public enum SelectMode
{
    /// <summary>The choices are mutually exclusive.</summary>
    One,

    /// <summary>Independent toggles.</summary>
    Any,

    /// <summary>A choice may be taken several times, up to its own max.</summary>
    Count,
}

/// <summary>Command group role.</summary>
public enum CommandRole
{
    Champion,
    Musician,
    StandardBearer,
}

public static class OptionKindExtensions
{
    private static readonly Dictionary<string, OptionKind> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["equipment"] = OptionKind.Equipment,
        ["mount"] = OptionKind.Mount,
        ["command"] = OptionKind.Command,
        ["magicStandard"] = OptionKind.MagicStandard,
        ["magicItems"] = OptionKind.MagicItems,
        ["battleStandard"] = OptionKind.BattleStandard,
        ["wizardLevel"] = OptionKind.WizardLevel,
        ["factionUpgrade"] = OptionKind.FactionUpgrade,
        ["specialRule"] = OptionKind.SpecialRule,
        ["upgrade"] = OptionKind.Upgrade,
    };

    private static readonly Dictionary<OptionKind, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this OptionKind kind) => ToJson[kind];

    public static bool TryFromJsonKey(string? jsonKey, out OptionKind result)
    {
        if (jsonKey != null && FromJson.TryGetValue(jsonKey, out result))
            return true;
        result = default;
        return false;
    }
}

public static class SelectModeExtensions
{
    public static SelectMode Parse(string? select) => select?.ToLowerInvariant() switch
    {
        "any" => SelectMode.Any,
        "count" => SelectMode.Count,
        _ => SelectMode.One,
    };
}

public static class CommandRoleExtensions
{
    /// <summary>
    /// The role a command group names. The bundle writes champion, musician or standardBearer;
    /// the books' own words — Leader, Boss, Standard Bearer — are accepted too so a hand-built
    /// fixture reads the same way.
    /// </summary>
    public static bool TryFromRoleText(string? roleText, out CommandRole result)
    {
        if (roleText is null)
        {
            result = default;
            return false;
        }

        if (roleText.Contains("champion", StringComparison.OrdinalIgnoreCase)
            || roleText.Contains("leader", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.Champion;
            return true;
        }

        if (roleText.Contains("musician", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.Musician;
            return true;
        }

        if (roleText.Contains("standard", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.StandardBearer;
            return true;
        }

        result = default;
        return false;
    }
}

public static class OptionGroupExtensions
{
    /// <summary>The group's kind, or null when the bundle names one this build does not know.</summary>
    public static OptionKind? GetKind(this OptionGroup group) =>
        OptionKindExtensions.TryFromJsonKey(group.Kind, out var kind) ? kind : null;

    public static bool IsKind(this OptionGroup group, OptionKind kind) => group.GetKind() == kind;

    public static SelectMode GetSelectMode(this OptionGroup group) => SelectModeExtensions.Parse(group.Select);

    public static bool IsRole(this OptionGroup group, CommandRole role) =>
        group.IsKind(OptionKind.Command)
        && CommandRoleExtensions.TryFromRoleText(group.Role, out var parsed)
        && parsed == role;

    /// <summary>
    /// Whether the group is offered to a given profile. A group that names no profile is offered
    /// to every one; a group that names one is offered only to it.
    /// </summary>
    public static bool AppliesToProfile(this OptionGroup group, string? profileId) =>
        group.AppliesTo is null
        || profileId is null
        || string.Equals(group.AppliesTo, profileId, StringComparison.OrdinalIgnoreCase);

    /// <summary>The same test for a choice's own restriction — "Griffon (General only)".</summary>
    public static bool AppliesToProfile(this OptionChoice choice, string? profileId) =>
        choice.AppliesTo is null
        || profileId is null
        || string.Equals(choice.AppliesTo, profileId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every group in a unit, at every nesting level, paired with the choice it hangs off — null
    /// for a top-level group. The walk the costing, the equipment resolution and the editor share.
    /// </summary>
    public static IEnumerable<(OptionGroup Group, OptionChoice? Parent)> Flatten(this IEnumerable<OptionGroup> groups)
    {
        foreach (var group in groups)
        {
            yield return (group, null);
            foreach (var choice in group.Choices)
            {
                if (choice.Options is null) continue;
                foreach (var nested in choice.Options.Flatten())
                    yield return (nested.Group, nested.Parent ?? choice);
            }
        }
    }
}
