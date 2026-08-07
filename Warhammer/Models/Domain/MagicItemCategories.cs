namespace Warhammer.Models.Domain;

/// <summary>
/// The six categories the magic item lists are printed under. The JSON keys come straight
/// from the army books; these turn them into something readable.
/// </summary>
public static class MagicItemCategories
{
    public const string MagicWeapon = "magic_weapon";
    public const string MagicArmour = "magic_armour";
    public const string Talisman = "talisman";
    public const string ArcaneItem = "arcane_item";
    public const string EnchantedItem = "enchanted_item";
    public const string MagicStandard = "magic_standard";

    private static readonly Dictionary<string, (string One, string Many)> Names = new()
    {
        [MagicWeapon] = ("magic weapon", "Magic Weapons"),
        [MagicArmour] = ("suit of magic armour", "Magic Armour"),
        [Talisman] = ("talisman", "Talismans"),
        [ArcaneItem] = ("arcane item", "Arcane Items"),
        [EnchantedItem] = ("enchanted item", "Enchanted Items"),
        [MagicStandard] = ("magic standard", "Magic Standards"),
    };

    /// <summary>Heading for a whole category, e.g. "Magic Weapons".</summary>
    public static string Plural(string category) =>
        Names.TryGetValue(category, out var name) ? name.Many : category;

    /// <summary>Name for a single item of the category, e.g. "magic weapon".</summary>
    public static string Singular(string category) =>
        Names.TryGetValue(category, out var name) ? name.One : category;
}
