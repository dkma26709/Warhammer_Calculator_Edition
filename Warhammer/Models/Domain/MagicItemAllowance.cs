namespace Warhammer.Models.Domain;

/// <summary>
/// What an entry may spend on magic items: the points, which item lists it may draw from
/// (null for any of the six), and which upgrade families the same allowance also pays for —
/// "one Virtue and/or Magic Items up to a total of 100 points".
/// </summary>
public sealed record MagicItemAllowance(
    int? PointsLimit,
    IReadOnlyList<string>? ItemTypes,
    IReadOnlyList<string> Families)
{
    /// <summary>Whether items of a category may be taken against this allowance.</summary>
    public bool Admits(string category) =>
        ItemTypes is null
        || ItemTypes.Count == 0
        || ItemTypes.Contains(category, StringComparer.OrdinalIgnoreCase);
}
