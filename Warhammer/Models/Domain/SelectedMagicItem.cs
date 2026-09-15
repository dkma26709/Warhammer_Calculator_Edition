namespace Warhammer.Models.Domain;

/// <summary>
/// A magic item selected by the user, with its manually-configured combat modifier.
/// Points cost is tracked for budget enforcement.
/// </summary>
public record SelectedMagicItem
{
    /// <summary>The item's bundle id, when it came from the data; null for a hand-entered item.</summary>
    public string? Id { get; init; }

    /// <summary>Item name as it appears in the army book.</summary>
    public required string Name { get; init; }

    /// <summary>Item category: weapon, armour, talisman, arcane, enchanted or standard.</summary>
    public required string Category { get; init; }

    /// <summary>Points cost of the item.</summary>
    public required double PointsCost { get; init; }

    /// <summary>The combat modifier this item applies. Configured by the user via the modifier editor.</summary>
    public UnitModifier Modifier { get; init; } = new();
}
