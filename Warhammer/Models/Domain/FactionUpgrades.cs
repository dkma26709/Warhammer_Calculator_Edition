using System.Text.RegularExpressions;
using Warhammer.Models.Json;

namespace Warhammer.Models.Domain;

/// <summary>
/// How a faction's upgrade chapter — Knightly Orders, Blessed Spawnings, Runic Items — is
/// addressed and priced. Option groups name a family by the chapter's kebab-case id; the
/// entries themselves carry the chapter's printed title.
/// </summary>
public static class FactionUpgrades
{
    private static readonly Regex PerModel =
        new(@"(?<n>\d+(?:\.\d+)?)\s*(?:points?|pts?)\s*(?:/|per)\s*model", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Flat =
        new(@"(?<n>\d+(?:\.\d+)?)\s*(?:points?|pts?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The family id an upgrade entry belongs to: its chapter title, slugged.</summary>
    public static string FamilyOf(UpgradeEntry entry) => Slug(entry.Chapter);

    /// <summary>
    /// Whether two family ids name the same chapter. An ampersand in a chapter title —
    /// "MUTATIONS &amp; TRAITS" — slugs to nothing, where the option groups spell it "and", so
    /// the word is dropped from both sides before comparing.
    /// </summary>
    public static bool SameFamily(string a, string b) =>
        string.Equals(Canonical(a), Canonical(b), StringComparison.OrdinalIgnoreCase);

    private static string Canonical(string family) =>
        Regex.Replace(family.ToLowerInvariant(), @"(^|-)and(-|$)", "-").Trim('-');

    /// <summary>The entries of a family, in the book's order.</summary>
    public static IEnumerable<UpgradeEntry> InFamily(IEnumerable<UpgradeEntry> upgrades, string family) =>
        upgrades.Where(u => SameFamily(FamilyOf(u), family));

    /// <summary>The chapter's printed title for a family, or the family id when no entry carries it.</summary>
    public static string FamilyTitle(IEnumerable<UpgradeEntry> upgrades, string family) =>
        InFamily(upgrades, family).FirstOrDefault()?.Chapter is { } chapter
            ? TitleCase(chapter)
            : family.Replace('-', ' ');

    /// <summary>
    /// What an entry costs the unit taking it. A flat price is a flat price. A tiered price —
    /// "Characters: free, Cavalry/Infantry: 1 point per model, Monstrous Cavalry: 3 points per
    /// model" — is read by matching the unit against the tier's scope: a character takes the
    /// Characters tier, anything else the tier naming its troop type.
    /// </summary>
    public static (double Points, bool PerModel) ResolveCost(UpgradeCost? cost, bool isCharacter, BaseTroopType troopType)
    {
        if (cost is null) return (0, false);
        if (cost.Flat is { } flat) return (flat, false);
        if (cost.Tiers.Count == 0) return (0, false);

        var tier = cost.Tiers.FirstOrDefault(t => Matches(t.Scope, isCharacter, troopType))
                   ?? cost.Tiers[0];
        return ParseCostText(tier.Text);
    }

    /// <summary>"free" → 0; "3 points per model" → (3, per model); "25 points" → (25, flat).</summary>
    public static (double Points, bool PerModel) ParseCostText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (0, false);
        if (text.Contains("free", StringComparison.OrdinalIgnoreCase)) return (0, false);

        var perModel = PerModel.Match(text);
        if (perModel.Success)
            return (Number(perModel.Groups["n"].Value), true);

        var flat = Flat.Match(text);
        if (flat.Success)
            return (Number(flat.Groups["n"].Value), false);

        return (0, false);
    }

    /// <summary>How a cost reads on screen: "free", "+25 pts", "+1 pt/model", or the tiers themselves.</summary>
    public static string Describe(UpgradeCost? cost)
    {
        if (cost is null) return "";
        if (cost.Flat is { } flat) return flat == 0 ? "free" : $"+{Format(flat)} pts";
        return string.Join("; ", cost.Tiers.Select(t => $"{t.Scope}: {t.Text}"));
    }

    private static bool Matches(string scope, bool isCharacter, BaseTroopType troopType)
    {
        var parts = scope.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("Character", StringComparison.OrdinalIgnoreCase))
            {
                if (isCharacter) return true;
                continue;
            }

            if (isCharacter) continue;

            var typeName = TypeName(troopType);
            if (string.Equals(part, typeName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string TypeName(BaseTroopType type) => type switch
    {
        BaseTroopType.Infantry => "Infantry",
        BaseTroopType.LargeInfantry => "Large Infantry",
        BaseTroopType.MonstrousInfantry => "Monstrous Infantry",
        BaseTroopType.Cavalry => "Cavalry",
        BaseTroopType.MonstrousCavalry => "Monstrous Cavalry",
        BaseTroopType.WarBeasts => "War Beasts",
        BaseTroopType.MonstrousBeasts => "Monstrous Beasts",
        BaseTroopType.Swarms => "Swarms",
        BaseTroopType.Monster => "Monster",
        BaseTroopType.Chariot => "Chariot",
        BaseTroopType.WarMachine => "War Machine",
        BaseTroopType.Shrine => "Shrine",
        _ => type.ToString(),
    };

    private static double Number(string text) =>
        double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(double value) =>
        value % 1 == 0 ? value.ToString("F0") : value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>kebab-case ASCII, as the bundle derives ids from printed names.</summary>
    public static string Slug(string text)
    {
        var slug = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return slug;
    }

    /// <summary>"KNIGHTLY ORDERS" → "Knightly Orders". A title with lower-case letters is left alone.</summary>
    public static string TitleCase(string text)
    {
        if (text.Any(char.IsLower)) return text;
        return string.Concat(text.Select((c, i) =>
            i > 0 && char.IsLetter(text[i - 1]) ? char.ToLowerInvariant(c) : c));
    }
}
