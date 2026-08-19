namespace Warhammer.Models.Json;

/// <summary>
/// Shape of <c>wwwroot/data/{system}/composition.json</c>. Hand-maintained, unlike
/// core-rules.json — a category absent from <see cref="Categories"/> has no such limit.
/// </summary>
public record CompositionData
{
    public string System { get; init; } = "";
    public double CharactersCombinedMaxPercent { get; init; }
    public double? SingleUnitMaxPercent { get; init; }
    public Dictionary<string, CategoryLimitEntry> Categories { get; init; } = [];
}

public record CategoryLimitEntry
{
    public double? MaxPercent { get; init; }
    public double? MinPercent { get; init; }
}
