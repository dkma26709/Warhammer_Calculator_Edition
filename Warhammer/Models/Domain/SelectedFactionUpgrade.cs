namespace Warhammer.Models.Domain;

/// <summary>
/// A faction upgrade pick (Blessed Spawning, Gift of Khaine, Discipline, ...).
/// </summary>
/// <param name="Name">Upgrade name as it appears in the army book.</param>
/// <param name="PointsCost">
/// Points listed against this upgrade in the faction's extras. Only used when the
/// unit's option carries no rate of its own — characters pay per upgrade, whereas
/// rank-and-file units pay their option's per-model rate instead.
/// </param>
public record SelectedFactionUpgrade(string Name, double PointsCost);
