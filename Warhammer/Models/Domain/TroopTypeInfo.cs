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

    /// <summary>Supporting attacks a rider (or a single-profile model) may make.</summary>
    public int MaxSupportingAttacks { get; init; }

    /// <summary>
    /// Supporting attacks the mount may make. Zero in wap-v3, which took them away from
    /// Cavalry and Monstrous Cavalry outright ("Mounts are not allowed to make supporting
    /// attacks", 3.11 p72–73).
    /// </summary>
    public int MountSupportingAttacks { get; init; }

    /// <summary>
    /// Supporting attacks for a model of this type that has no split profile — the books
    /// state a separate figure, and it is not simply rider plus mount.
    /// </summary>
    public int NonSplitSupportingAttacks { get; init; }

    /// <summary>
    /// Impact Hits per front-rank model conferred by the troop type alone, before any unit
    /// special rule. wap-v2 gives Cavalry and Monstrous Cavalry Impact Hits (1); wap-v3
    /// removed it from both, leaving Chariots to carry theirs on the unit entry.
    /// </summary>
    public int InherentImpactHits { get; init; }

    public int UnitStrength { get; init; }
    public bool HasSplitProfile { get; init; }

    /// <summary>
    /// Swiftstride from the troop type alone. wap-v2 grants it to most mounted and monstrous
    /// types; wap-v3 dropped it from every troop type and put Independent in its place.
    /// </summary>
    public bool HasInherentSwiftstride { get; init; }

    public static TroopTypeInfo Parse(string? troopTypeString, string? systemKey = null)
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

        var props = GetTypeProperties(baseType, systemKey == GameSystems.WapV3);

        return new TroopTypeInfo
        {
            BaseType = baseType,
            IsCharacter = isCharacter,
            IntrinsicArmourSave = intrinsicArmour,
            MinRankWidth = props.MinWidth,
            MaxSupportingAttacks = props.MaxSupport,
            MountSupportingAttacks = props.MountSupport,
            NonSplitSupportingAttacks = props.NonSplitSupport,
            InherentImpactHits = props.ImpactHits,
            UnitStrength = props.UnitStr,
            HasSplitProfile = props.SplitProfile,
            HasInherentSwiftstride = props.Swiftstride,
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

    private readonly record struct TypeProperties(
        int MinWidth, int MaxSupport, int MountSupport, int NonSplitSupport,
        int ImpactHits, int UnitStr, bool SplitProfile, bool Swiftstride);

    /// <summary>
    /// The Troop Types chapter, p71–76 in both books. The two editions diverge in three
    /// places: supporting attacks for the monstrous types, mount supporting attacks, and the
    /// inherent special rules — wap-v3 removed Impact Hits from Cavalry and Monstrous Cavalry
    /// and Swiftstride from every type, replacing it with Independent, which is a
    /// unit-joining rule and has no combat effect here.
    /// wap-v3 also drops Large Infantry as a type; the wap-v2 row is kept for any entry that
    /// still names it.
    /// </summary>
    private static TypeProperties GetTypeProperties(BaseTroopType type, bool v3) => type switch
    {
        //                                     width  rider  mount  nonSplit  impact  US  split  swift
        BaseTroopType.Infantry          => new(    5,     1,     0,        1,      0,  1, false, false),
        BaseTroopType.LargeInfantry     => new(    4,     2,     0,        2,      0,  1, false, false),
        BaseTroopType.MonstrousInfantry => v3
                                         ? new(    3,     3,     0,        3,      0,  2, false, false)
                                         : new(    3,     2,     0,        2,      0,  2, false, true),
        BaseTroopType.Cavalry           => v3
                                         ? new(    5,     1,     0,        1,      0,  2, true,  false)
                                         : new(    5,     1,     1,        2,      1,  2, true,  true),
        BaseTroopType.MonstrousCavalry  => v3
                                         ? new(    3,     1,     0,        3,      0,  3, true,  false)
                                         : new(    3,     1,     2,        3,      1,  3, true,  true),
        BaseTroopType.WarBeasts         => new(    5,     1,     0,        1,      0,  1, false, !v3),
        BaseTroopType.MonstrousBeasts   => v3
                                         ? new(    3,     3,     0,        3,      0,  2, false, false)
                                         : new(    3,     2,     0,        2,      0,  2, false, true),
        BaseTroopType.Swarms            => new(    3,     5,     0,        5,      0,  3, false, false),
        BaseTroopType.Monster           => new(    1,     0,     0,        0,      0,  3, false, !v3),
        BaseTroopType.Chariot           => new(    1,     0,     0,        0,      0,  3, false, !v3),
        BaseTroopType.WarMachine        => new(    1,     0,     0,        0,      0,  3, false, false),
        BaseTroopType.Shrine            => new(    1,     0,     0,        0,      0,  3, false, false),
        _                               => new(    5,     1,     0,        1,      0,  1, false, false),
    };

    private static TroopTypeInfo DefaultInfantry() => new()
    {
        BaseType = BaseTroopType.Infantry,
        MinRankWidth = 5,
        MaxSupportingAttacks = 1,
        NonSplitSupportingAttacks = 1,
        UnitStrength = 1,
        HasSplitProfile = false
    };
}
