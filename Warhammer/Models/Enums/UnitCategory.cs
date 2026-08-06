namespace Warhammer.Models.Enums;

/// <summary>
/// Army book unit category.
/// </summary>
public enum UnitCategory
{
    Lords,
    Heroes,
    Characters,

    /// <summary>
    /// The army books' SPECIAL CHARACTERS section. Kept apart from Lords and Heroes rather than
    /// split between them: the books do distinguish "SPECIAL CHARACTERS (LORDS)" from "(HEROES)",
    /// but only in the table of contents — 12 of the 15 wap-v2 books and 2 wap-v3 books do so,
    /// while the section's own running header is a plain "SPECIAL CHARACTERS" on every page, and
    /// that header is what the extractor reads. Recovering the split therefore means parsing the
    /// contents page ranges, not adding a category, so until then binning these into Lords or
    /// Heroes would be a guess. They are still characters: the combined characters cap, the
    /// single-character cap and General eligibility all apply.
    /// </summary>
    NamedCharacters,
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
        ["Named Characters"] = UnitCategory.NamedCharacters,
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
        [UnitCategory.NamedCharacters] = "Named Characters",
        [UnitCategory.Core] = "Core",
        [UnitCategory.Special] = "Special",
        [UnitCategory.Rare] = "Rare",
        [UnitCategory.CharacterMounts] = "Character Mounts",
    };

    public static string ToJsonKey(this UnitCategory key) => ToJson[key];

    public static bool TryFromJsonKey(string jsonKey, out UnitCategory result)
        => FromJson.TryGetValue(jsonKey, out result);

    /// <summary>
    /// Every category that holds characters: v2's Lords/Heroes, the merged v3 Characters, and the
    /// books' SPECIAL CHARACTERS section. Anything summing "all characters" — the combined cap in
    /// <c>ArmyListValidator</c> and the Builder's summary — reads this rather than listing the
    /// categories again, so the two cannot drift apart.
    /// </summary>
    public static readonly UnitCategory[] CharacterCategories =
    [
        UnitCategory.Lords,
        UnitCategory.Heroes,
        UnitCategory.Characters,
        UnitCategory.NamedCharacters,
    ];

    /// <summary>True for categories that hold characters — see <see cref="CharacterCategories"/>.</summary>
    public static bool IsCharacterCategory(this UnitCategory category)
        => Array.IndexOf(CharacterCategories, category) >= 0;
}
