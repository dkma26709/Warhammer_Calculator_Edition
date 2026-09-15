using Warhammer.Models.Enums;

namespace Warhammer.Models.Domain;

public sealed record ArmyList
{
    /// <summary>The faction's bundle id — see <see cref="Faction.Id"/>.</summary>
    public required string FactionId { get; init; }

    public required int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<UnitConfiguration> Units { get; init; } = new();

    /// <summary>House-ruleset adjustments to the system's composition percentages.</summary>
    public CompositionOverrides Overrides { get; set; } = new();

    public double TotalPoints => Units.Sum(u => u.TotalPointsCost);

    public double PointsInCategory(UnitCategory category) =>
        Units.Where(u => u.ResolvedUnit.Category == category).Sum(u => u.TotalPointsCost);
}
