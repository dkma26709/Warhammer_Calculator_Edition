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

public static class BaseTroopTypeExtensions
{
    private static readonly Dictionary<BaseTroopType, string> ToJson = new()
    {
        [BaseTroopType.Infantry] = "infantry",
        [BaseTroopType.LargeInfantry] = "large-infantry",
        [BaseTroopType.MonstrousInfantry] = "monstrous-infantry",
        [BaseTroopType.Cavalry] = "cavalry",
        [BaseTroopType.MonstrousCavalry] = "monstrous-cavalry",
        [BaseTroopType.WarBeasts] = "war-beasts",
        [BaseTroopType.MonstrousBeasts] = "monstrous-beasts",
        [BaseTroopType.Swarms] = "swarms",
        [BaseTroopType.Monster] = "monster",
        [BaseTroopType.Chariot] = "chariot",
        [BaseTroopType.WarMachine] = "war-machine",
        [BaseTroopType.Shrine] = "shrine",
    };

    public static string ToJsonKey(this BaseTroopType type) => ToJson[type];
}

/// <summary>
/// The figures the Troop Types chapter states for one troop type, resolved from the active
/// system's <c>troop-types.json</c>.
/// </summary>
public readonly record struct TroopTypeRules(
    int MinRankWidth,
    int SupportingAttacks,
    int MountSupportingAttacks,
    int NonSplitSupportingAttacks,
    int ImpactHits,
    int UnitStrength,
    bool SplitProfile,
    bool Swiftstride);

public record TroopTypeInfo
{
    public BaseTroopType BaseType { get; init; }
    public bool IsCharacter { get; init; }
    public int? IntrinsicArmourSave { get; init; }
    public int MinRankWidth { get; init; }

    /// <summary>
    /// Supporting attacks the rider may make. For a model with no mount profile the engine
    /// uses <see cref="NonSplitSupportingAttacks"/> instead, which can be a different figure.
    /// </summary>
    public int MaxSupportingAttacks { get; init; }

    /// <summary>
    /// Supporting attacks the mount may make. Split-profile types only, and zero throughout
    /// wap-v3, which bars mounts from supporting attacks.
    /// </summary>
    public int MountSupportingAttacks { get; init; }

    /// <summary>
    /// The figure the books state for a model of this type without a split profile. It is not
    /// derived from the rider and mount figures — wap-v3 Monstrous Cavalry is 1 + 0 for a
    /// split profile but 3 without one.
    /// </summary>
    public int NonSplitSupportingAttacks { get; init; }

    /// <summary>
    /// Impact Hits per front-rank model conferred by the troop type alone, before any unit
    /// special rule. wap-v2 gives Cavalry and Monstrous Cavalry one each; wap-v3 gives none
    /// to any type, leaving Chariots to carry theirs on the unit entry.
    /// </summary>
    public int InherentImpactHits { get; init; }

    public int UnitStrength { get; init; }
    public bool HasSplitProfile { get; init; }

    /// <summary>
    /// Swiftstride from the troop type alone. wap-v2 grants it to Cavalry, Monstrous Cavalry,
    /// Monstrous Infantry, Monstrous Beasts, War Beasts, Monsters and Chariots; no wap-v3
    /// troop type grants it.
    /// </summary>
    public bool HasInherentSwiftstride { get; init; }

    /// <summary>
    /// Parse a <c>troopType</c> string, taking the per-system figures from a resolver.
    /// </summary>
    /// <param name="troopTypeString">The raw field, e.g. "Chariot (Armour Save 4+)".</param>
    /// <param name="rules">
    /// Supplies the active system's figures for a base type. <see cref="GameDataRepository"/>
    /// resolves these from <c>troop-types.json</c>; pass null only where the figures are
    /// irrelevant, which leaves them all zero.
    /// </param>
    public static TroopTypeInfo Parse(string? troopTypeString, Func<BaseTroopType, TroopTypeRules>? rules)
    {
        if (string.IsNullOrWhiteSpace(troopTypeString))
            return Resolve(BaseTroopType.Infantry, isCharacter: false, intrinsicArmour: null, rules);

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

        return Resolve(baseType, isCharacter, intrinsicArmour, rules);
    }

    private static TroopTypeInfo Resolve(
        BaseTroopType baseType, bool isCharacter, int? intrinsicArmour,
        Func<BaseTroopType, TroopTypeRules>? rules)
    {
        var figures = rules?.Invoke(baseType) ?? default;

        return new TroopTypeInfo
        {
            BaseType = baseType,
            IsCharacter = isCharacter,
            IntrinsicArmourSave = intrinsicArmour,
            MinRankWidth = figures.MinRankWidth,
            MaxSupportingAttacks = figures.SupportingAttacks,
            MountSupportingAttacks = figures.MountSupportingAttacks,
            NonSplitSupportingAttacks = figures.NonSplitSupportingAttacks,
            InherentImpactHits = figures.ImpactHits,
            UnitStrength = figures.UnitStrength,
            HasSplitProfile = figures.SplitProfile,
            HasInherentSwiftstride = figures.Swiftstride,
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

}
