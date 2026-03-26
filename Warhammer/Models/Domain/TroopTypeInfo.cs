using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

public enum BaseTroopType
{
    Infantry,
    LargeInfantry,
    MonstrousInfantry,
    Cavalry,
    MonstrousCavalry,
    WarBeasts,
    MonstrousBeasts,
    Swarms,
    Monster,
    Chariot,
    WarMachine,
    Shrine
}

public record TroopTypeInfo
{
    public BaseTroopType BaseType { get; init; }
    public bool IsCharacter { get; init; }
    public int? IntrinsicArmourSave { get; init; }
    public int MinRankWidth { get; init; }
    public int MaxSupportingAttacks { get; init; }
    public int UnitStrength { get; init; }
    public bool HasSplitProfile { get; init; }

    /// <summary>
    /// Whether this troop type has inherent Swiftstride (Cavalry, Monstrous Cavalry,
    /// Monstrous Infantry, Monstrous Beasts, War Beasts).
    /// </summary>
    public bool HasInherentSwiftstride => BaseType is
        BaseTroopType.Cavalry or BaseTroopType.MonstrousCavalry or
        BaseTroopType.MonstrousInfantry or BaseTroopType.MonstrousBeasts or
        BaseTroopType.WarBeasts;

    public static TroopTypeInfo Parse(string? troopTypeString)
    {
        if (string.IsNullOrWhiteSpace(troopTypeString))
            return DefaultInfantry();

        var cleaned = troopTypeString.TrimEnd('.', ' ');
        var match = Regex.Match(cleaned, @"^(.+?)(?:\s*\((.+)\))?$");

        var baseTypeStr = match.Groups[1].Value.Trim();
        var flags = match.Groups[2].Success
            ? match.Groups[2].Value.Split(',').Select(f => f.Trim()).ToList()
            : new List<string>();

        // Normalize typos and alternate names
        baseTypeStr = baseTypeStr switch
        {
            "Mosntrous Beast" => "Monstrous Beast",
            "Monstrous Creature" => "Monstrous Beast",
            _ => baseTypeStr
        };

        var baseType = ParseBaseType(baseTypeStr);
        bool isCharacter = flags.Any(f =>
            f.Contains("Character", StringComparison.OrdinalIgnoreCase));

        // Intrinsic armour save from "(Armour Save 4+)"
        int? intrinsicArmour = null;
        var armourFlag = flags.FirstOrDefault(f =>
            f.StartsWith("Armour Save", StringComparison.OrdinalIgnoreCase));
        if (armourFlag != null)
        {
            var saveMatch = Regex.Match(armourFlag, @"(\d)\+");
            if (saveMatch.Success)
                intrinsicArmour = int.Parse(saveMatch.Groups[1].Value);
        }

        var (minWidth, maxSupport, unitStr, splitProfile) = GetTypeProperties(baseType);

        return new TroopTypeInfo
        {
            BaseType = baseType,
            IsCharacter = isCharacter,
            IntrinsicArmourSave = intrinsicArmour,
            MinRankWidth = minWidth,
            MaxSupportingAttacks = maxSupport,
            UnitStrength = unitStr,
            HasSplitProfile = splitProfile
        };
    }

    private static BaseTroopType ParseBaseType(string s) => s.ToLowerInvariant() switch
    {
        "infantry" => BaseTroopType.Infantry,
        "large infantry" => BaseTroopType.LargeInfantry,
        "monstrous infantry" => BaseTroopType.MonstrousInfantry,
        "cavalry" => BaseTroopType.Cavalry,
        "monstrous cavalry" => BaseTroopType.MonstrousCavalry,
        "war beast" or "war beasts" => BaseTroopType.WarBeasts,
        "monstrous beast" or "monstrous beasts" => BaseTroopType.MonstrousBeasts,
        "swarm" or "swarms" => BaseTroopType.Swarms,
        "monster" => BaseTroopType.Monster,
        "chariot" => BaseTroopType.Chariot,
        "war machine" => BaseTroopType.WarMachine,
        "shrine" => BaseTroopType.Shrine,
        _ => BaseTroopType.Infantry
    };

    private static (int MinWidth, int MaxSupport, int UnitStr, bool SplitProfile) GetTypeProperties(BaseTroopType type) => type switch
    {
        BaseTroopType.Infantry => (5, 1, 1, false),
        BaseTroopType.LargeInfantry => (4, 2, 1, false),
        BaseTroopType.MonstrousInfantry => (3, 2, 2, false),
        BaseTroopType.Cavalry => (5, 1, 2, true),
        BaseTroopType.MonstrousCavalry => (3, 2, 3, true),
        BaseTroopType.WarBeasts => (5, 1, 1, false),
        BaseTroopType.MonstrousBeasts => (3, 2, 2, false),
        BaseTroopType.Swarms => (3, 5, 3, false),
        BaseTroopType.Monster => (1, 0, 3, false),
        BaseTroopType.Chariot => (1, 0, 3, false),
        BaseTroopType.WarMachine => (1, 0, 3, false),
        BaseTroopType.Shrine => (1, 0, 3, false),
        _ => (5, 1, 1, false)
    };

    private static TroopTypeInfo DefaultInfantry() => new()
    {
        BaseType = BaseTroopType.Infantry,
        MinRankWidth = 5,
        MaxSupportingAttacks = 1,
        UnitStrength = 1,
        HasSplitProfile = false
    };
}
