using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

/// <summary>
/// A fully-resolved armour piece with pre-parsed save modifiers.
/// Immutable — created once during data loading.
/// </summary>
public sealed record Armour
{
    public required ArmourKey Key { get; init; }
    public required string Name { get; init; }

    /// <summary>Save modifier for close combat (e.g., 3 for heavy armour "+3/4+").</summary>
    public required int CombatSaveModifier { get; init; }

    /// <summary>Standalone close combat save value (e.g., 4 for heavy armour "+3/4+").</summary>
    public required int CombatStandaloneSave { get; init; }

    /// <summary>Save modifier for missile attacks.</summary>
    public required int MissileSaveModifier { get; init; }

    /// <summary>Standalone missile save value.</summary>
    public required int MissileStandaloneSave { get; init; }

    /// <summary>Parry ward save value, if this armour grants one (e.g., 6 for shield "Parry (6+)").</summary>
    public int? ParryValue { get; init; }

    /// <summary>
    /// Parse an armour save field "+N/M+" into (modifier, standaloneSave).
    /// E.g., "+3/4+" → (3, 4). Returns (0, 7) if unparseable.
    /// </summary>
    public static (int Modifier, int StandaloneSave) ParseSaveField(string saveField)
    {
        var match = System.Text.RegularExpressions.Regex.Match(saveField, @"\+(\d)/(\d)\+");
        if (match.Success)
        {
            int modifier = int.Parse(match.Groups[1].Value);
            int standalone = int.Parse(match.Groups[2].Value);
            return (modifier, standalone);
        }

        return (0, 7);
    }

    /// <summary>
    /// Parse a "Parry (N+)" string from armour special rules. Returns null if none found.
    /// </summary>
    public static int? ParseParryValue(string? specialRules)
    {
        if (string.IsNullOrWhiteSpace(specialRules))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(specialRules, @"Parry\s*\((\d)\+\)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }
}
