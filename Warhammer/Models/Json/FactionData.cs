using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

public record FactionData
{
    public string FactionName { get; init; } = "";
    public List<UnitEntry> Units { get; init; } = [];
}

public record UnitEntry
{
    public string Name { get; init; } = "";
    public string Category { get; set; } = "";
    public List<ProfileEntry> Profiles { get; init; } = [];
    public string TroopType { get; init; } = "";
    public string? BaseSize { get; init; }
    public string? Equipment { get; init; }
    public List<UnitOption>? Options { get; init; }
    public string? SpecialRules { get; init; }
    public string? UnitSize { get; init; }
    public double? PointsCost { get; init; }
    public string? Magic { get; init; }
    public string? Upgrades { get; init; }
    public string? Notes { get; init; }
    public string? Crew { get; init; }
    public string? Mount { get; init; }
    public string? DrawnBy { get; init; }
}

public record ProfileEntry
{
    public string Name { get; init; } = "";

    [JsonConverter(typeof(NullableIntConverter))]
    public int? M { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? WS { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? BS { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? S { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? T { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? W { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? I { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? A { get; init; }

    [JsonConverter(typeof(NullableIntConverter))]
    public int? Ld { get; init; }

    public double? Points { get; init; }
}
