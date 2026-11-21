using System.Globalization;
using System.Text.RegularExpressions;
using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

/// <summary>
/// What a faction upgrade costs one unit: a flat figure, or a rate charged for every model.
/// </summary>
/// <param name="Points">The figure; zero is free.</param>
/// <param name="PerModel">True when <paramref name="Points"/> is charged once per model.</param>
public readonly record struct UpgradePrice(double Points, bool PerModel)
{
    private const string Characters = "Characters";

    /// <summary>The troop types a tiered price can name, as the army books write them.</summary>
    private static readonly string[] TierNames =
        [Characters, "Infantry", "Cavalry", "Monstrous Cavalry", "Monstrous Infantry"];

    /// <summary>
    /// One line of a tiered price — "Cavalry/Infantry 1 point per model", "Characters 10 points",
    /// "Monstrous Cavalry free". The books also write "1 points per model" and "0.5 point per model".
    /// </summary>
    private static readonly Regex TierLine = new(
        @"^(?<tiers>[A-Za-z ]+(?:/[A-Za-z ]+)*)\s+(?:(?<free>free)|(?<n>\d+(?:\.\d+)?)\s+points?(?<per>\s+per\s+model)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The price as a picker shows it: "free", "10 pts", "1 pt/model".</summary>
    public string Label
    {
        get
        {
            if (Points == 0) return "free";
            var n = Points.ToString(CultureInfo.InvariantCulture);
            if (PerModel) return $"{n} pt/model";
            return Points == 1 ? "1 pt" : $"{n} pts";
        }
    }

    /// <summary>
    /// The price an upgrade's points text sets for one unit. Blank text is free and a plain
    /// number is a flat price for every unit. Tiered text ("Characters free\nCavalry 1 point
    /// per model") prices the line naming the unit's troop type, and is null — not offered —
    /// when no line does. Any other text is free, as it always was.
    /// </summary>
    public static UpgradePrice? Resolve(string? pointsText, TroopTypeInfo troopType, UnitCategory category)
    {
        if (string.IsNullOrWhiteSpace(pointsText)) return new UpgradePrice(0, false);
        if (TryParseNumber(pointsText, out var points)) return new UpgradePrice(points, false);

        var tiers = ParseTiers(pointsText);
        if (tiers is null) return new UpgradePrice(0, false);

        var tier = TierFor(troopType, category);
        if (tier is null) return null;
        foreach (var (names, price) in tiers)
        {
            if (!names.Contains(tier, StringComparer.OrdinalIgnoreCase)) continue;
            // A character is one model whatever it rides, so its line is a flat price.
            return tier == Characters ? price with { PerModel = false } : price;
        }
        return null;
    }

    /// <summary>True when the text is a price keyed by troop type rather than one figure.</summary>
    public static bool IsTiered(string? pointsText) => ParseTiers(pointsText) is not null;

    /// <summary>True when the text is blank, a plain number or fully parsed tiered text.</summary>
    public static bool IsRecognised(string? pointsText) =>
        string.IsNullOrWhiteSpace(pointsText) || TryParseNumber(pointsText, out _) || IsTiered(pointsText);

    /// <summary>
    /// The tier a unit buys at. A character takes the Characters line whatever its base type or
    /// mount; any other unit its base troop type, and a type the books never price has no tier.
    /// </summary>
    private static string? TierFor(TroopTypeInfo troopType, UnitCategory category)
    {
        if (troopType.IsCharacter || category.IsCharacterCategory()) return Characters;
        return troopType.BaseType switch
        {
            BaseTroopType.Infantry => "Infantry",
            BaseTroopType.Cavalry => "Cavalry",
            BaseTroopType.MonstrousCavalry => "Monstrous Cavalry",
            BaseTroopType.MonstrousInfantry => "Monstrous Infantry",
            _ => null,
        };
    }

    private static bool TryParseNumber(string text, out double points) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out points);

    /// <summary>
    /// Every line of a tiered price, or null unless each non-blank line names only known tiers
    /// and states an amount — a half-understood price is not trusted for any unit.
    /// </summary>
    private static List<(string[] Names, UpgradePrice Price)>? ParseTiers(string? pointsText)
    {
        if (string.IsNullOrWhiteSpace(pointsText)) return null;

        var tiers = new List<(string[] Names, UpgradePrice Price)>();
        foreach (var raw in pointsText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var match = TierLine.Match(line);
            if (!match.Success) return null;

            var names = match.Groups["tiers"].Value
                .Split('/')
                .Select(n => Regex.Replace(n.Trim(), @"\s+", " "))
                .ToArray();
            if (!names.All(n => TierNames.Contains(n, StringComparer.OrdinalIgnoreCase))) return null;

            var price = match.Groups["free"].Success
                ? new UpgradePrice(0, false)
                : new UpgradePrice(
                    double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture),
                    match.Groups["per"].Success);
            tiers.Add((names, price));
        }
        return tiers.Count > 0 ? tiers : null;
    }
}
