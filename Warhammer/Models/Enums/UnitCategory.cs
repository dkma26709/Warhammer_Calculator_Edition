namespace Warhammer.Models.Enums;

/// <summary>
/// Army book unit category.
/// </summary>
public enum UnitCategory
{
    Lords,
    Heroes,
    Characters,
    Core,
    Special,
    Rare,
    CharacterMounts,
}

public static class UnitCategoryExtensions
{
    private static readonly Dictionary<string, UnitCategory> FromJson = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lords"] = UnitCategory.Lords,
        ["Heroes"] = UnitCategory.Heroes,
        ["Characters"] = UnitCategory.Characters,
        ["Core"] = UnitCategory.Core,
        ["Special"] = UnitCategory.Special,
        ["Rare"] = UnitCategory.Rare,
        ["Character Mounts"] = UnitCategory.CharacterMounts,
        ["CHARACTER MOUNTS"] = UnitCategory.CharacterMounts,
    };

    private static readonly Dictionary<UnitCategory, string> ToJson = new()
    {
        [UnitCategory.Lords] = "Lords",
        [UnitCategory.Heroes] = "Heroes",
        [UnitCategory.Characters] = "Characters",
        [UnitCategory.Core] = "Core",
        [UnitCategory.Special] = "Special",
        [UnitCategory.Rare] = "Rare",
        [UnitCategory.CharacterMounts] = "Character Mounts",
    };

    public static string ToJsonKey(this UnitCategory key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out UnitCategory result)
        => FromJson.TryGetValue(jsonKey, out result);

    /// <summary>True for categories that hold characters: v2 Lords/Heroes and the merged v3 Characters.</summary>
    public static bool IsCharacterCategory(this UnitCategory category)
        => category is UnitCategory.Lords or UnitCategory.Heroes or UnitCategory.Characters;
}
