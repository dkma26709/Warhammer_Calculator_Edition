namespace Warhammer.Models.Enums;

/// <summary>
/// Unit option category from faction JSON.
/// </summary>
public enum OptionType
{
    Equipment,
    Command,
    Mount,
    MagicItems,
    WizardUpgrade,
    Upgrade,
    BlessedSpawning,
    BattleStandard,
    MagicStandard,
}

/// <summary>
/// Equipment option action type.
/// </summary>
public enum OptionAction
{
    Choose,
    Add,
    Replace,
}

/// <summary>
/// Command group role.
/// </summary>
public enum CommandRole
{
    Champion,
    Musician,
    StandardBearer,
}

public static class OptionTypeExtensions
{
    private static readonly Dictionary<string, OptionType> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["equipment"] = OptionType.Equipment,
        ["command"] = OptionType.Command,
        ["mount"] = OptionType.Mount,
        ["magic_items"] = OptionType.MagicItems,
        ["wizard_upgrade"] = OptionType.WizardUpgrade,
        ["upgrade"] = OptionType.Upgrade,
        ["blessed_spawning"] = OptionType.BlessedSpawning,
        ["battle_standard"] = OptionType.BattleStandard,
        ["magic_standard"] = OptionType.MagicStandard,
    };

    private static readonly Dictionary<OptionType, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this OptionType key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out OptionType result)
        => FromJson.TryGetValue(jsonKey, out result);
}

public static class OptionActionExtensions
{
    private static readonly Dictionary<string, OptionAction> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["choose"] = OptionAction.Choose,
        ["add"] = OptionAction.Add,
        ["replace"] = OptionAction.Replace,
    };

    private static readonly Dictionary<OptionAction, string> ToJson = FromJson
        .ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string ToJsonKey(this OptionAction key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out OptionAction result)
        => FromJson.TryGetValue(jsonKey, out result);
}

public static class CommandRoleExtensions
{
    public static bool TryFromRoleText(string roleText, out CommandRole result)
    {
        if (roleText.Contains("Leader", StringComparison.OrdinalIgnoreCase)
            || roleText.Contains("Champion", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.Champion;
            return true;
        }

        if (roleText.Contains("Musician", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.Musician;
            return true;
        }

        if (roleText.Contains("Standard Bearer", StringComparison.OrdinalIgnoreCase))
        {
            result = CommandRole.StandardBearer;
            return true;
        }

        result = default;
        return false;
    }
}
