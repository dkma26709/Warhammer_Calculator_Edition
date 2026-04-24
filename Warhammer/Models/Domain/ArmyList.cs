using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

public sealed record ArmyList
{
    public required FactionKey Faction { get; init; }
    public required int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<UnitConfiguration> Units { get; init; } = new();

    public double TotalPoints => Units.Sum(u => u.TotalPointsCost);

    public double PointsInCategory(UnitCategory category) =>
        Units.Where(u => u.ResolvedUnit.Category == category).Sum(u => u.TotalPointsCost);
}
