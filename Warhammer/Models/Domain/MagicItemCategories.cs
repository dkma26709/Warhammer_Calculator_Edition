namespace Warhammer.Models.Domain;

/// <summary>
/// The category keys magic items carry in the bundle — the six lists every army book prints.
/// Dwarf runes are not items in this format: they are an upgrade family, "runic-items", and
/// answer to <see cref="IsMasterRune"/> from there.
/// </summary>
public static class MagicItemCategories
{
    public const string Weapon = "weapon";
    public const string Armour = "armour";
    public const string Talisman = "talisman";
    public const string Arcane = "arcane";
    public const string Enchanted = "enchanted";
    public const string Standard = "standard";

    /// <summary>The six printed lists, in the order the books print them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Weapon, Armour, Talisman, Arcane, Enchanted, Standard,
    ];

    private static readonly Dictionary<string, (string One, string Many)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        [Weapon] = ("magic weapon", "Magic Weapons"),
        [Armour] = ("suit of magic armour", "Magic Armour"),
        [Talisman] = ("talisman", "Talismans"),
        [Arcane] = ("arcane item", "Arcane Items"),
        [Enchanted] = ("enchanted item", "Enchanted Items"),
        [Standard] = ("magic standard", "Magic Standards"),
    };

    /// <summary>
    /// Whether a model may hold only one item of this category. True of the six printed lists,
    /// and of anything unrecognised — an imported list can carry any category string, and a
    /// category nobody wrote is held to the limit rather than quietly escaping it.
    /// </summary>
    public static bool HasOnePerModelLimit(string category) => true;

    /// <summary>
    /// Whether an item or rune is a master rune, which may be used only once per army. The army
    /// books name every one of them "Master Rune of ...".
    /// </summary>
    public static bool IsMasterRune(string itemName) =>
        itemName.StartsWith("Master Rune", StringComparison.OrdinalIgnoreCase);

    /// <summary>Heading for a whole category, e.g. "Magic Weapons".</summary>
    public static string Plural(string category) =>
        Names.TryGetValue(category, out var name) ? name.Many : category;

    /// <summary>Name for a single item of the category, e.g. "magic weapon".</summary>
    public static string Singular(string category) =>
        Names.TryGetValue(category, out var name) ? name.One : category;

    /// <summary>Whether the key is one of the six printed lists.</summary>
    public static bool IsKnown(string category) => Names.ContainsKey(category);
}
