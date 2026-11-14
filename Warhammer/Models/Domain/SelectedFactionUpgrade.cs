namespace Warhammer.Models.Domain;

/// <summary>
/// A faction upgrade pick (Blessed Spawning, Gift of Khaine, Discipline, Knightly Order, ...).
/// </summary>
/// <param name="Name">Upgrade name as it appears in the army book.</param>
/// <param name="PointsCost">
/// Points listed against this upgrade in the faction's extras, resolved for the unit taking it
/// (see <see cref="UpgradePrice.Resolve"/>) — for a cumulative rune, the total for all
/// <paramref name="Copies"/>. Only used when the unit's option carries no rate of its own —
/// rank-and-file units with a rate on the option pay that per-model rate instead.
/// </param>
/// <param name="PerModel">
/// True when <paramref name="PointsCost"/> is charged per model, as a tiered price such as a
/// Knightly Order's "Cavalry 1 point per model" is; false for a flat price.
/// </param>
/// <param name="Copies">
/// How many of this upgrade the unit takes: one, or up to the figure count of a cumulative
/// rune's "15/25/50" (see <see cref="UpgradePrice.CumulativeTotal"/>).
/// </param>
public record SelectedFactionUpgrade(string Name, double PointsCost, bool PerModel = false, int Copies = 1);
