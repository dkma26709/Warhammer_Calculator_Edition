using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>
/// Handles options arrays that may contain strings (most factions)
/// or structured objects (lizardmen-new, the-empire-new).
/// For objects, extracts the "raw" text plus each "choices[].name" as separate strings.
/// </summary>
public class OptionsListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartArray)
            return null;

        var result = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.String)
            {
                result.Add(reader.GetString() ?? "");
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Parse the object, extract raw + choice names
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;

                if (root.TryGetProperty("raw", out var rawProp))
                    result.Add(rawProp.GetString() ?? "");

                if (root.TryGetProperty("choices", out var choicesProp) &&
                    choicesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choicesProp.EnumerateArray())
                    {
                        if (choice.TryGetProperty("name", out var nameProp))
                        {
                            // name can be a string or an array of strings
                            var name = nameProp.ValueKind == JsonValueKind.Array
                                ? string.Join(", ", nameProp.EnumerateArray().Select(e => e.GetString() ?? ""))
                                : nameProp.GetString() ?? "";
                            var pointsStr = "";
                            if (choice.TryGetProperty("points", out var ptsProp) && ptsProp.ValueKind == JsonValueKind.Number)
                            {
                                // Handle both int and float points (e.g. 0.5)
                                if (ptsProp.TryGetInt32(out var intPts))
                                    pointsStr = intPts.ToString();
                                else
                                    pointsStr = ptsProp.GetDouble().ToString("G");
                            }
                            result.Add($"{name} +{pointsStr} points");
                        }
                    }
                }
            }
        }

        return result.Count > 0 ? result : null;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}
