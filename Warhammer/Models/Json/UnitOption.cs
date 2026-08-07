using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

/// <summary>
/// A single upgrade option for a unit, deserialized from faction JSON.
/// </summary>
public record UnitOption
{
    /// <summary>
    /// Option category: equipment, command, mount, magic_items, wizard_upgrade,
    /// upgrade, blessed_spawning, faction_upgrade, battle_standard, magic_standard.
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// factionUpgrades category this option offers (e.g. blessed_spawnings, gifts_of_khaine).
    /// Set for faction_upgrade options; each pick costs the points listed against it in the
    /// faction's extras rather than a cost on the option itself.
    /// </summary>
    public string? FactionUpgrade { get; init; }

    /// <summary>For equipment options: choose, add, or replace.</summary>
    public string? Action { get; init; }

    /// <summary>For replace actions: the base equipment item being replaced.</summary>
    public string? Replaces { get; init; }

    /// <summary>Available choices within this option group.</summary>
    public List<OptionChoice>? Choices { get; init; }

    /// <summary>For command options: Leader, Musician, or Standard Bearer.</summary>
    public string? Role { get; init; }

    /// <summary>Flat points cost (command, upgrade, wizard_upgrade).</summary>
    public double? Points { get; init; }

    /// <summary>Whether cost is per model (upgrade, blessed_spawning).</summary>
    public bool? PerModel { get; init; }

    /// <summary>Points limit for magic items or magic standards.</summary>
    public int? PointsLimit { get; init; }

    /// <summary>
    /// The entry this option belongs to, when the army book named one ("A Captain may take
    /// Magic Items up to a total of 50 points"). A single options block often covers several
    /// entries, so every one of them carries all the block's options; this is what tells them
    /// apart. Null when the line was written without a name, which means it applies to whoever
    /// holds it.
    /// </summary>
    public string? AppliesTo { get; init; }

    /// <summary>Descriptive name for wizard_upgrade or upgrade options.</summary>
    public string? Upgrade { get; init; }

    /// <summary>Nested options (e.g., magic standard under standard bearer).</summary>
    public List<UnitOption>? SubOptions { get; init; }

    /// <summary>Original text from the army book.</summary>
    public string? Raw { get; init; }
}

/// <summary>
/// A single choice within an option group.
/// </summary>
public record OptionChoice
{
    /// <summary>
    /// Item name. Usually a string, but can be an array of strings for multi-item
    /// choices (e.g., ["javelin", "shield"]).
    /// </summary>
    [JsonConverter(typeof(OptionChoiceNameConverter))]
    public List<string> Name { get; init; } = [];

    /// <summary>Points cost for this choice (null if not specified in army book).</summary>
    public double? Points { get; init; }

    /// <summary>Whether cost is per model.</summary>
    public bool PerModel { get; init; }

    /// <summary>For replace choices: what this specific choice replaces.</summary>
    public string? Replaces { get; init; }

    /// <summary>Display name: joins multi-item names with " & ".</summary>
    [JsonIgnore]
    public string DisplayName => string.Join(" & ", Name);
}

/// <summary>
/// Handles the Name field which can be either a string or an array of strings in JSON.
/// </summary>
public class OptionChoiceNameConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return [reader.GetString() ?? ""];

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var result = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                    result.Add(reader.GetString() ?? "");
            }

            return result;
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            writer.WriteStringValue(value[0]);
        }
        else
        {
            writer.WriteStartArray();
            foreach (var item in value)
                writer.WriteStringValue(item);
            writer.WriteEndArray();
        }
    }
}
