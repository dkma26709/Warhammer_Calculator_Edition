namespace Warhammer.Models.Enums;

/// <summary>
/// Identifies a close combat weapon from core-rules.json.
/// </summary>
public enum CloseCombatWeaponKey
{
    HandWeapon,
    TwoAdditionalHandWeapons,
    Polearm,
    GreatWeapon,
    Flail,
    Spear,
    Pike,
    LightLance,
    HeavyLance,
}

public static class CloseCombatWeaponKeyExtensions
{
    private static readonly Dictionary<string, CloseCombatWeaponKey> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["handWeapon"] = CloseCombatWeaponKey.HandWeapon,
        ["two_additionalHandWeapons"] = CloseCombatWeaponKey.TwoAdditionalHandWeapons,
        ["polearm"] = CloseCombatWeaponKey.Polearm,
        ["greatWeapon"] = CloseCombatWeaponKey.GreatWeapon,
        ["flail"] = CloseCombatWeaponKey.Flail,
        ["spear"] = CloseCombatWeaponKey.Spear,
        ["pike"] = CloseCombatWeaponKey.Pike,
        ["lightLance"] = CloseCombatWeaponKey.LightLance,
        ["heavyLance"] = CloseCombatWeaponKey.HeavyLance,
    };

    private static readonly Dictionary<CloseCombatWeaponKey, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this CloseCombatWeaponKey key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out CloseCombatWeaponKey result)
        => FromJson.TryGetValue(jsonKey, out result);
}
