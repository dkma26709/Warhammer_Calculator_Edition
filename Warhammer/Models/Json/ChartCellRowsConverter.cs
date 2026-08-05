using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>
/// Reads a combat chart's rows where each cell may be a string ("4+", "6+/3+", "n/a") or a
/// bare number (4), normalising both to strings.
/// </summary>
/// <remarks>
/// The extractors emit the book's own notation, but earlier core-rules.json files stored plain
/// ints. Accepting both means a data file written before the change still loads rather than
/// silently deserialising to an empty table — the failure mode that let a null to-wound chart
/// reach a render and crash it.
/// </remarks>
public class ChartCellRowsConverter : JsonConverter<string[][]>
{
    public override string[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return [];

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Combat chart table must be an array of rows.");

        var rows = new List<string[]>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.Null)
            {
                rows.Add([]);
                continue;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Each combat chart row must be an array of cells.");

            rows.Add(ReadRow(ref reader));
        }

        return rows.ToArray();
    }

    private static string[] ReadRow(ref Utf8JsonReader reader)
    {
        var cells = new List<string>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.EndArray:
                    return cells.ToArray();
                case JsonTokenType.String:
                    cells.Add(reader.GetString() ?? "");
                    break;
                case JsonTokenType.Number:
                    cells.Add(reader.GetInt32().ToString());
                    break;
                case JsonTokenType.Null:
                    cells.Add("");
                    break;
                default:
                    throw new JsonException($"Unexpected {reader.TokenType} in a combat chart row.");
            }
        }

        throw new JsonException("Combat chart row was not terminated.");
    }

    public override void Write(Utf8JsonWriter writer, string[][] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var row in value)
        {
            writer.WriteStartArray();
            foreach (var cell in row)
                writer.WriteStringValue(cell);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}
