using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Bundle;

/// <summary>
/// A points figure the books print as a number, "-" for a profile that is not bought on its
/// own (a mount, a crew member), or occasionally a word. Anything that is not a number reads
/// as null rather than failing the whole faction.
/// </summary>
public class NullableDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(text)) return null;
                if (string.Equals(text, "free", StringComparison.OrdinalIgnoreCase)) return 0;
                return double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

/// <summary>
/// A field the bundle writes as a string for most entries and as a list for a few: a
/// special-rules line the book printed as bullets is a list of strings, and one the book wrote
/// out as definitions — "Militia: Models with this special rule can…" — is a list of
/// {name, text} objects. Read as one string, the names joined with commas, which is the form
/// every parser downstream expects.
/// </summary>
public class StringOrStringListConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.StartArray:
                var parts = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        // A list element the book wrote as a definition — "Altar of Hashut: A
                        // Sorcerer-Prophet with…" — names its rule before the colon.
                        var element = reader.GetString() ?? "";
                        var colon = element.IndexOf(": ", StringComparison.Ordinal);
                        parts.Add(colon is > 1 and < 80 ? element[..colon] : element);
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        using var element = JsonDocument.ParseValue(ref reader);
                        if (element.RootElement.TryGetProperty("name", out var name)
                            && name.ValueKind == JsonValueKind.String
                            && name.GetString() is { Length: > 0 } text)
                            parts.Add(text);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
                return parts.Count == 0 ? null : string.Join(", ", parts);
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>
/// A value that is a number for some entries and a string for others — a spell's level is
/// 1 to 4 for a proper spell and "Signature Spell" or "Lore Attribute" for the two that sit
/// outside the levels. Read as text either way.
/// </summary>
public class StringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var whole)
                    ? whole.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>
/// A field the bundle writes in whatever shape the book gave it — an item's <c>bound</c> spell
/// is null for most items, <c>true</c> for some and an object naming the spell for others.
/// Read as text: a string as is, a number or boolean spelled out, an object by its name or
/// spell property (else its JSON), an array joined with commas.
/// </summary>
public class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var whole)
                    ? whole.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            case JsonTokenType.True:
                return "true";
            case JsonTokenType.False:
                return null;
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
            {
                using var element = JsonDocument.ParseValue(ref reader);
                foreach (var key in new[] { "name", "spell", "text" })
                {
                    if (element.RootElement.TryGetProperty(key, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && value.GetString() is { Length: > 0 } text)
                        return text;
                }
                return element.RootElement.GetRawText();
            }
            case JsonTokenType.StartArray:
            {
                var parts = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                        parts.Add(reader.GetString() ?? "");
                    else
                        reader.Skip();
                }
                return parts.Count == 0 ? null : string.Join(", ", parts);
            }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>
/// A list the bundle writes with strings for most entries and objects for some — a unit's
/// notes carry a {name, text} pair where the book set a heading. Each element is read the way
/// <see cref="LenientStringConverter"/> reads a single value; a bare string is a list of one.
/// </summary>
public class LenientStringListConverter : JsonConverter<List<string>?>
{
    private static readonly LenientStringConverter Element = new();

    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartArray:
            {
                var items = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var item = Element.Read(ref reader, typeof(string), options);
                    if (!string.IsNullOrWhiteSpace(item)) items.Add(item);
                }
                return items;
            }
            default:
            {
                var single = Element.Read(ref reader, typeof(string), options);
                return string.IsNullOrWhiteSpace(single) ? null : [single];
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value) writer.WriteStringValue(item);
        writer.WriteEndArray();
    }
}

/// <summary>
/// An upgrade's cost as the bundle prints it: a number for a flat price, null when the entry
/// prices nothing, or a list of [scope, text] pairs when the price depends on who takes it —
/// the Empire's Knightly Orders are "Characters: free, Cavalry/Infantry: 1 point per model".
/// </summary>
public class UpgradeCostConverter : JsonConverter<UpgradeCost?>
{
    public override UpgradeCost? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return new UpgradeCost { Flat = reader.GetDouble() };
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim() ?? "";
                if (string.Equals(text, "free", StringComparison.OrdinalIgnoreCase))
                    return new UpgradeCost { Flat = 0 };
                return double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                    ? new UpgradeCost { Flat = value }
                    : new UpgradeCost { Tiers = [new UpgradeCostTier { Scope = "", Text = text }] };
            case JsonTokenType.StartArray:
                var tiers = new List<UpgradeCostTier>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        reader.Skip();
                        continue;
                    }

                    var cells = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        cells.Add(reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : "");
                    }

                    if (cells.Count >= 2)
                        tiers.Add(new UpgradeCostTier { Scope = cells[0], Text = cells[1] });
                }

                return new UpgradeCost { Tiers = tiers };
            case JsonTokenType.Null:
                return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, UpgradeCost? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else if (value.Flat is { } flat)
        {
            writer.WriteNumberValue(flat);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var tier in value.Tiers)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(tier.Scope);
                writer.WriteStringValue(tier.Text);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
}
