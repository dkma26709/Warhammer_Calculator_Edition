using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

/// <summary>
/// Matches the model name an army book puts in a rule's scope — the "War Boars" of
/// "Mighty Blow (2) (War Boars only)" — against a unit's profile names.
/// </summary>
/// <remarks>
/// The two never quite agree. The book pluralises ("War Boars" against a "War Boar" profile),
/// abbreviates ("Zlaaq" against "Zlaaq (Stegadon)"), and qualifies ("Orc brute" against a "Brute"
/// profile, "Salamander" against "Ancient Salamander"), so an exact comparison matches barely half
/// of them. Every scope in the shipped data is a model name — all 70 of them — and
/// ScopedRuleTests asserts that each resolves to exactly one profile, which is what keeps this
/// scoring honest rather than merely plausible.
/// </remarks>
public static class ModelScope
{
    private static readonly Regex TrailingAside = new(@"\s*\([^)]*\)\s*$", RegexOptions.Compiled);
    private static readonly Regex OnlyWord = new(@"\bonly\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonAlphanumeric = new(@"[^a-z0-9 ]", RegexOptions.Compiled);

    /// <summary>
    /// How strongly <paramref name="scope"/> names <paramref name="profileName"/>: 0 for not at
    /// all, higher for a better fit. Compare candidates and take the strongest — an exact match
    /// must beat a containment one, or "Slave Master only" picks the "Slave" profile it also
    /// contains rather than the "Slave Master" it names.
    /// </summary>
    public static int MatchStrength(string? scope, string? profileName)
    {
        var a = Normalise(scope);
        var b = Normalise(profileName);
        if (a.Length == 0 || b.Length == 0)
            return 0;

        if (a == b)
            return 100;

        // The books do not always agree with a profile on where the spaces go: wap-v3 spells the
        // Dark Elf Slaves' champion "Slavemaster" while the rule says "Slave Master only". Ignoring
        // spaces has to outrank containment below, or that scope settles for the "Slave" profile.
        if (a.Replace(" ", "") == b.Replace(" ", ""))
            return 90;

        // One is the tail of the other on a word boundary: "orc brute" against "brute".
        if (a.EndsWith(' ' + b, StringComparison.Ordinal) || b.EndsWith(' ' + a, StringComparison.Ordinal))
            return 50 + b.Length;

        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
            return b.Length;

        return 0;
    }

    /// <summary>Index of the candidate the scope names best, or null when it names none.</summary>
    public static int? BestMatch(string? scope, IReadOnlyList<string?> candidates)
    {
        int bestIndex = -1;
        int best = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            int strength = MatchStrength(scope, candidates[i]);
            if (strength > best)
            {
                best = strength;
                bestIndex = i;
            }
        }
        return bestIndex >= 0 ? bestIndex : null;
    }

    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = TrailingAside.Replace(value.Trim().ToLowerInvariant(), "");
        text = OnlyWord.Replace(text, "");
        text = NonAlphanumeric.Replace(text, " ");
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return Singular(text);
    }

    /// <summary>Drop a plural 's' so "war boars" and "war boar" compare equal.</summary>
    private static string Singular(string text) =>
        text.EndsWith('s') && !text.EndsWith("ss", StringComparison.Ordinal)
            ? text[..^1]
            : text;
}
