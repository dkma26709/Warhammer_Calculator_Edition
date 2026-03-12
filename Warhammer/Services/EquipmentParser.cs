namespace Warhammer.Services;

public static class EquipmentParser
{
    private static readonly Dictionary<string, (string Key, bool IsWeapon)> NormMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hand weapon"] = ("handWeapon", true),
            ["hand weapons"] = ("handWeapon", true),
            ["additional hand weapon"] = ("two_additionalHandWeapons", true),
            ["two additional hand weapons"] = ("two_additionalHandWeapons", true),
            ["two hand weapons"] = ("two_additionalHandWeapons", true),
            ["great weapon"] = ("greatWeapon", true),
            ["great weapons"] = ("greatWeapon", true),
            ["spear"] = ("spear", true),
            ["spears"] = ("spear", true),
            ["pike"] = ("pike", true),
            ["pikes"] = ("pike", true),
            ["polearm"] = ("polearm", true),
            ["polearms"] = ("polearm", true),
            ["light lance"] = ("lightLance", true),
            ["heavy lance"] = ("heavyLance", true),
            ["lance"] = ("heavyLance", true),
            ["flail"] = ("flail", true),
            ["flails"] = ("flail", true),
            ["morning star"] = ("flail", true),
            ["shortbow"] = ("shortbow", true),
            ["short bow"] = ("shortbow", true),
            ["bow"] = ("bow", true),
            ["bows"] = ("bow", true),
            ["longbow"] = ("longbow", true),
            ["long bow"] = ("longbow", true),
            ["warbow"] = ("warbow", true),
            ["crossbow"] = ("crossbow", true),
            ["crossbows"] = ("crossbow", true),
            ["handgun"] = ("handgun", true),
            ["handguns"] = ("handgun", true),
            ["pistol"] = ("pistol", true),
            ["brace of pistols"] = ("pistol", true),
            ["javelin"] = ("javelins", true),
            ["javelins"] = ("javelins", true),
            ["throwing weapons"] = ("throwingWeapons", true),
            ["throwing axes"] = ("throwingAxes", true),
            ["sling"] = ("sling", true),
            ["blowpipe"] = ("blowpipe", true),
            ["blowpipes"] = ("blowpipe", true),
            ["light armour"] = ("lightArmour", false),
            ["medium armour"] = ("mediumArmour", false),
            ["heavy armour"] = ("heavyArmour", false),
            ["shield"] = ("shields", false),
            ["shields"] = ("shields", false),
            ["buckler"] = ("buckler", false),
            ["bucklers"] = ("buckler", false),
            ["barding"] = ("barding", false),
        };

    /// <summary>
    /// Try to normalize an option choice name to a core-rules key.
    /// Returns false if the name is not recognized.
    /// </summary>
    public static bool TryNormalize(string name, out string key, out bool isWeapon)
    {
        if (NormMap.TryGetValue(name.Trim().TrimEnd('.'), out var mapped))
        {
            key = mapped.Key;
            isWeapon = mapped.IsWeapon;
            return true;
        }

        key = "";
        isWeapon = false;
        return false;
    }

    /// <summary>
    /// Check if a normalized key is a close combat weapon (used in the combat engine).
    /// </summary>
    public static bool IsCloseCombatWeaponKey(string key)
    {
        return key is "handWeapon" or "two_additionalHandWeapons" or "polearm"
            or "greatWeapon" or "flail" or "spear" or "pike" or "lightLance" or "heavyLance";
    }

    /// <summary>
    /// Parse equipment text into weapon and armour keys matching core-rules.json dictionary keys.
    /// Only parses the first line (before any \n).
    /// </summary>
    public static (List<string> WeaponKeys, List<string> ArmourKeys) Parse(string? equipment)
    {
        var weapons = new List<string>();
        var armour = new List<string>();
        if (string.IsNullOrWhiteSpace(equipment)) return (weapons, armour);

        // Stop at \n — anything after is extended description/magic items
        var mainLine = equipment.Split('\n')[0];

        foreach (var item in mainLine.Split(','))
        {
            var trimmed = item.Trim().TrimEnd('.');
            if (NormMap.TryGetValue(trimmed, out var mapped))
            {
                if (mapped.IsWeapon)
                    weapons.Add(mapped.Key);
                else
                    armour.Add(mapped.Key);
            }
        }

        return (weapons, armour);
    }
}
