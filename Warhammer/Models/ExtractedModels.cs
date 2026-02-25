namespace Warhammer.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public class ExtractedFactionData
{
    [JsonPropertyName("factionName")]
    public string FactionName { get; set; } = "";

    [JsonPropertyName("units")]
    public List<ExtractedUnit> Units { get; set; } = new();
}

public class ExtractedUnit
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("profiles")]
    public List<ExtractedProfile> Profiles { get; set; } = new();

    [JsonPropertyName("troopType")]
    public string? TroopType { get; set; }

    [JsonPropertyName("baseSize")]
    public string? BaseSize { get; set; }

    [JsonPropertyName("equipment")]
    public string? Equipment { get; set; }

    [JsonPropertyName("magic")]
    public string? Magic { get; set; }

    [JsonPropertyName("options")]
    public List<string> Options { get; set; } = new();

    [JsonPropertyName("specialRules")]
    public string? SpecialRules { get; set; }

    [JsonPropertyName("upgrades")]
    public string? Upgrades { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("pointsCost")]
    public int? PointsCost { get; set; }

    [JsonPropertyName("unitSize")]
    public string? UnitSize { get; set; }

    [JsonPropertyName("crew")]
    public string? Crew { get; set; }

    [JsonPropertyName("mount")]
    public string? Mount { get; set; }

    [JsonPropertyName("drawnBy")]
    public string? DrawnBy { get; set; }

    public ExtractedProfile PrimaryProfile => Profiles.FirstOrDefault(p => p.Points.HasValue) ?? Profiles[0];

    public ExtractedProfile? MountProfile => Profiles.Count > 1 ? Profiles[1] : null;

    public int ParseMinUnitSize()
    {
        if (string.IsNullOrEmpty(UnitSize)) return 1;
        var match = System.Text.RegularExpressions.Regex.Match(UnitSize, @"(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    /// <summary>
    /// Parse frontage width in mm from the baseSize field (e.g. "20x20 or 25x25" → 20).
    /// </summary>
    public int BaseWidthMm
    {
        get
        {
            if (string.IsNullOrEmpty(BaseSize)) return 25;
            var match = System.Text.RegularExpressions.Regex.Match(BaseSize, @"(\d+)x\d+");
            return match.Success ? int.Parse(match.Groups[1].Value) : 25;
        }
    }
}

public class ExtractedProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("M")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? M { get; set; }

    [JsonPropertyName("WS")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? WS { get; set; }

    [JsonPropertyName("BS")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? BS { get; set; }

    [JsonPropertyName("S")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? S { get; set; }

    [JsonPropertyName("T")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? T { get; set; }

    [JsonPropertyName("W")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? W { get; set; }

    [JsonPropertyName("I")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? I { get; set; }

    [JsonPropertyName("A")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? A { get; set; }

    [JsonPropertyName("Ld")]
    [JsonConverter(typeof(NullableIntFromJsonConverter))]
    public int? Ld { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }
}

/// <summary>
/// Handles JSON values that can be int, null, or string (like "*" for variable stats).
/// </summary>
public class NullableIntFromJsonConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32();
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (int.TryParse(str, out var val))
                return val;
            return null; // "*", "-", etc.
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
