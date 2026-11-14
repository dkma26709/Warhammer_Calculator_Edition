using Warhammer.Models.Json;

namespace Warhammer.Models.Domain;

/// <summary>
/// What a magic item costs the model taking it. Most items have one price; a cumulative Dwarf
/// rune ("15/25/50") charges each further copy on the same model the next figure, and allows no
/// more copies than it has figures. Each copy is its own <see cref="SelectedMagicItem"/>.
/// </summary>
public static class MagicItemPricing
{
    /// <summary>The price of one copy: the first figure of cumulative points, otherwise the number; unparsed text is free.</summary>
    public static double FirstCopyCost(string? points) => UpgradePrice.CumulativeTotal(points, 1);

    /// <summary>
    /// What the next copy of <paramref name="entry"/> costs a model already holding
    /// <paramref name="held"/>, or null when it holds as many copies as a cumulative rune has
    /// figures. Any other item costs its one price however many the model holds.
    /// </summary>
    public static double? NextCopyCost(IEnumerable<SelectedMagicItem> held, MagicItemEntry entry)
    {
        if (!UpgradePrice.TryParseCumulative(entry.Points, out var perCopy))
            return FirstCopyCost(entry.Points);

        var copies = held.Count(i => i.Name == entry.Name);
        return copies < perCopy.Count ? perCopy[copies] : null;
    }

    /// <summary>
    /// Price the copies of <paramref name="entry"/> in <paramref name="items"/> again in list
    /// order, so after a copy is removed the rest cost the first, second ... figures. Does
    /// nothing for an item that is not a cumulative rune.
    /// </summary>
    public static void RepriceCopies(List<SelectedMagicItem> items, MagicItemEntry entry)
    {
        if (!UpgradePrice.TryParseCumulative(entry.Points, out var perCopy)) return;

        var copy = 0;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Name != entry.Name) continue;
            // A hand-edited list can hold more copies than figures; those keep what they were saved at.
            if (copy < perCopy.Count) items[i] = items[i] with { PointsCost = perCopy[copy] };
            copy++;
        }
    }
}
