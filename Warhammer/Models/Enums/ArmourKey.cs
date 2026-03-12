namespace Warhammer.Models.Enums;

/// <summary>
/// Identifies an armour piece from core-rules.json.
/// </summary>
public enum ArmourKey
{
    LightArmour,
    MediumArmour,
    HeavyArmour,
    Shields,
    Buckler,
    Barding,
}

public static class ArmourKeyExtensions
{
    private static readonly Dictionary<string, ArmourKey> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lightArmour"] = ArmourKey.LightArmour,
        ["mediumArmour"] = ArmourKey.MediumArmour,
        ["heavyArmour"] = ArmourKey.HeavyArmour,
        ["shields"] = ArmourKey.Shields,
        ["buckler"] = ArmourKey.Buckler,
        ["barding"] = ArmourKey.Barding,
    };

    private static readonly Dictionary<ArmourKey, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this ArmourKey key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out ArmourKey result)
        => FromJson.TryGetValue(jsonKey, out result);
}
