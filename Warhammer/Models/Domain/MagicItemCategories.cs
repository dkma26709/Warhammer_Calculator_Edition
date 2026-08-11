namespace Warhammer.Models.Domain;

/// <summary>
/// The category keys magic items carry in the data. Six are the lists every army book prints.
/// Five more are the Dwarf runes, which sit in the same item lists but answer to their own
/// rules — see <see cref="HasOnePerModelLimit"/> and <see cref="IsMasterRune"/>.
/// </summary>
public static class MagicItemCategories
{
    public const string MagicWeapon = "magic_weapon";
    public const string MagicArmour = "magic_armour";
    public const string Talisman = "talisman";
    public const string ArcaneItem = "arcane_item";
    public const string EnchantedItem = "enchanted_item";
    public const string MagicStandard = "magic_standard";

    public const string WeaponRune = "weapon_rune";
    public const string ArmourRune = "armour_rune";
    public const string TalismanicRune = "talismanic_rune";
    public const string BannerRune = "banner_rune";
    public const string EngineeringRune = "engineering_rune";

    /// <summary>
    /// The rune categories, which are the exception rather than the rule. Kept as the exception
    /// list rather than listing the six printed categories, so a category the data starts
    /// carrying tomorrow — a hand-edited saved list, a renamed key after a re-extraction — is
    /// held to the one-per-model limit instead of quietly escaping it.
    /// </summary>
    private static readonly HashSet<string> Runes =
    [
        WeaponRune, ArmourRune, TalismanicRune, BannerRune, EngineeringRune,
    ];

    private static readonly Dictionary<string, (string One, string Many)> Names = new()
    {
        [MagicWeapon] = ("magic weapon", "Magic Weapons"),
        [MagicArmour] = ("suit of magic armour", "Magic Armour"),
        [Talisman] = ("talisman", "Talismans"),
        [ArcaneItem] = ("arcane item", "Arcane Items"),
        [EnchantedItem] = ("enchanted item", "Enchanted Items"),
        [MagicStandard] = ("magic standard", "Magic Standards"),
        [WeaponRune] = ("weapon rune", "Weapon Runes"),
        [ArmourRune] = ("armour rune", "Armour Runes"),
        [TalismanicRune] = ("talismanic rune", "Talismanic Runes"),
        [BannerRune] = ("banner rune", "Banner Runes"),
        [EngineeringRune] = ("engineering rune", "Engineering Runes"),
    };

    /// <summary>
    /// Whether a model may hold only one item of this category. True of the six printed lists,
    /// and of anything unrecognised. False only for runes: a Dwarf may inscribe up to three on
    /// one item, and apart from master runes they combine freely.
    /// </summary>
    public static bool HasOnePerModelLimit(string category) => !Runes.Contains(category);

    /// <summary>
    /// Whether an item is a master rune, which may be used only once per army. The army books
    /// name every one of them "Master Rune of ...".
    /// </summary>
    public static bool IsMasterRune(string itemName) =>
        itemName.StartsWith("Master Rune", StringComparison.OrdinalIgnoreCase);

    /// <summary>Heading for a whole category, e.g. "Magic Weapons".</summary>
    public static string Plural(string category) =>
        Names.TryGetValue(category, out var name) ? name.Many : category;

    /// <summary>Name for a single item of the category, e.g. "magic weapon".</summary>
    public static string Singular(string category) =>
        Names.TryGetValue(category, out var name) ? name.One : category;
}
