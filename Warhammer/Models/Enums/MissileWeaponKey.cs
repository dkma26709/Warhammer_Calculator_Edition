namespace Warhammer.Models.Enums;

/// <summary>
/// Identifies a missile weapon from core-rules.json.
/// </summary>
public enum MissileWeaponKey
{
    Shortbow,
    Bow,
    Longbow,
    Warbow,
    Greatbow,
    Crossbow,
    Handgun,
    Blunderbuss,
    Pistol,
    Javelins,
    ThrowingWeapons,
    ThrowingAxes,
    Sling,
    Blowpipe,
    BoltThrower,
    RepeaterBoltThrower,
}

public static class MissileWeaponKeyExtensions
{
    private static readonly Dictionary<string, MissileWeaponKey> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shortbow"] = MissileWeaponKey.Shortbow,
        ["bow"] = MissileWeaponKey.Bow,
        ["longbow"] = MissileWeaponKey.Longbow,
        ["warbow"] = MissileWeaponKey.Warbow,
        ["greatbow"] = MissileWeaponKey.Greatbow,
        ["crossbow"] = MissileWeaponKey.Crossbow,
        ["handgun"] = MissileWeaponKey.Handgun,
        ["blunderbuss"] = MissileWeaponKey.Blunderbuss,
        ["pistol"] = MissileWeaponKey.Pistol,
        ["javelins"] = MissileWeaponKey.Javelins,
        ["throwingWeapons"] = MissileWeaponKey.ThrowingWeapons,
        ["throwingAxes"] = MissileWeaponKey.ThrowingAxes,
        ["sling"] = MissileWeaponKey.Sling,
        ["blowpipe"] = MissileWeaponKey.Blowpipe,
        ["boltThrower"] = MissileWeaponKey.BoltThrower,
        ["repeaterBoltThrower"] = MissileWeaponKey.RepeaterBoltThrower,
    };

    private static readonly Dictionary<MissileWeaponKey, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this MissileWeaponKey key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out MissileWeaponKey result)
        => FromJson.TryGetValue(jsonKey, out result);
}
