namespace Warhammer.Models.Enums;

/// <summary>
/// Army list section a unit fills. The bundle states it as <c>section</c> — characters, core,
/// special, rare, mounts — with <c>tier</c> and <c>named</c> beside it; this enum folds the
/// three into one value the Builder can group and the validator can sum.
/// </summary>
public enum UnitCategory
{
    /// <summary>A character in a book that splits its characters by tier.</summary>
    Lords,

    /// <summary>A character in a book that splits its characters by tier.</summary>
    Heroes,

    /// <summary>A character in a book with one characters section.</summary>
    Characters,

    /// <summary>
    /// The books' SPECIAL CHARACTERS section. Kept apart from Lords and Heroes: the running header
    /// is a plain "SPECIAL CHARACTERS" and the bundle marks these <c>named</c> rather than tiered.
    /// They are still characters: the combined characters cap, the single-unit cap and General
    /// eligibility all apply.
    /// </summary>
    NamedCharacters,
    Core,
    Special,
    Rare,

    /// <summary>The book's mounts section: profiles a character's mount option points at.</summary>
    CharacterMounts,
}

public static class UnitCategoryExtensions
{
    private static readonly Dictionary<UnitCategory, string> Names = new()
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

    private static readonly Dictionary<string, UnitCategory> FromName = Names
        .ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>How the category reads on screen and in saved lists.</summary>
    public static string ToJsonKey(this UnitCategory key) => Names[key];

    public static bool TryFromJsonKey(string jsonKey, out UnitCategory result)
        => FromName.TryGetValue(jsonKey, out result);

    /// <summary>
    /// The category for a bundle unit. A named character is one whatever its tier; a tiered
    /// character is a Lord or Hero; any other character is a plain Character. Null for a section
    /// the app does not field — the bundle's null section, for a chapter that is none of the five.
    /// </summary>
    public static UnitCategory? FromSection(string? section, string? tier, bool named)
    {
        switch (section?.ToLowerInvariant())
        {
            case "characters":
                if (named) return UnitCategory.NamedCharacters;
                return tier?.ToLowerInvariant() switch
                {
                    "lord" => UnitCategory.Lords,
                    "hero" => UnitCategory.Heroes,
                    _ => UnitCategory.Characters,
                };
            case "core":
                return UnitCategory.Core;
            case "special":
                return UnitCategory.Special;
            case "rare":
                return UnitCategory.Rare;
            case "mounts":
                return UnitCategory.CharacterMounts;
            default:
                return null;
        }
    }

    /// <summary>
    /// Every category that holds characters. Anything summing "all characters" — the combined cap
    /// in <c>ArmyListValidator</c> and the Builder's summary — reads this rather than listing the
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

    /// <summary>The composition section a category counts toward: characters, core, special or rare.</summary>
    public static string ToSectionKey(this UnitCategory category) => category switch
    {
        UnitCategory.Core => "core",
        UnitCategory.Special => "special",
        UnitCategory.Rare => "rare",
        UnitCategory.CharacterMounts => "mounts",
        _ => "characters",
    };
}
