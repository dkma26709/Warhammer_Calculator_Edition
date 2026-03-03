using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

public class NullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetInt32();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (str == "*" || string.IsNullOrWhiteSpace(str))
                    return null;
                if (int.TryParse(str, out var val))
                    return val;
                return null;
            case JsonTokenType.Null:
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
