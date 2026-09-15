namespace Warhammer.Models.Domain;

/// <summary>
/// A faction upgrade pick (a Knightly Order, a Blessed Spawning, a rune, a Virtue).
/// </summary>
/// <param name="Id">The upgrade entry's id in the faction's upgrade chapter.</param>
/// <param name="Name">Upgrade name as it appears in the army book.</param>
/// <param name="PointsCost">
/// What this pick costs the entry, resolved when it was chosen: the entry's flat price, or
/// the tier that applies to the unit taking it. Only used when the unit's own option carries
/// no rate — rank-and-file units pay their option's per-model rate instead.
/// </param>
public record SelectedFactionUpgrade(string Id, string Name, double PointsCost);
