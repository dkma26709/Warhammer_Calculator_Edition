namespace Warhammer.Models;

using System.Text.RegularExpressions;

public enum BaseTroopType
{
    Infantry,
    Cavalry,
    MonstrousInfantry,
    MonstrousCavalry,
    MonstrousBeast,
    WarBeast,
    Swarm,
    Monster,
    Chariot,
    Shrine,
    WarMachine,
    Unknown
}

public record ParsedTroopType(
    BaseTroopType BaseType,
    bool IsCharacter,
    string SubType,
    int? IntrinsicArmourSave
);

public static class TroopTypeParser
{
    public static ParsedTroopType Parse(string? troopTypeText)
    {
        if (string.IsNullOrWhiteSpace(troopTypeText))
            return new ParsedTroopType(BaseTroopType.Unknown, false, "", null);

        var isCharacter = troopTypeText.Contains("Character", StringComparison.OrdinalIgnoreCase);

        var parenIndex = troopTypeText.IndexOf('(');
        var baseText = (parenIndex > 0 ? troopTypeText[..parenIndex] : troopTypeText).Trim();

        var baseType = baseText.ToLowerInvariant().Replace(" ", "") switch
        {
            "infantry" or "largeinfantry" => BaseTroopType.Infantry,
            "cavalry" => BaseTroopType.Cavalry,
            "monstrousinfantry" => BaseTroopType.MonstrousInfantry,
            "monstrouscavalry" => BaseTroopType.MonstrousCavalry,
            "monstrousbeast" or "monstrouscreature" => BaseTroopType.MonstrousBeast,
            "warbeast" => BaseTroopType.WarBeast,
            "swarm" => BaseTroopType.Swarm,
            "monster" => BaseTroopType.Monster,
            "chariot" => BaseTroopType.Chariot,
            "shrine" => BaseTroopType.Shrine,
            "warmachine" => BaseTroopType.WarMachine,
            _ => BaseTroopType.Unknown
        };

        string subType = "";
        int? intrinsicArmour = null;

        if (parenIndex > 0)
        {
            var closeIndex = troopTypeText.IndexOf(')');
            if (closeIndex > parenIndex)
            {
                var content = troopTypeText[(parenIndex + 1)..closeIndex];

                var armourMatch = Regex.Match(content, @"[Aa]rmour\s+[Ss]ave\s+(\d)\+");
                if (armourMatch.Success)
                {
                    intrinsicArmour = int.Parse(armourMatch.Groups[1].Value);
                }
                else
                {
                    var parts = content.Split(',')
                        .Select(p => p.Trim())
                        .Where(p => !p.Equals("Character", StringComparison.OrdinalIgnoreCase));
                    subType = string.Join(", ", parts);
                }
            }
        }

        return new ParsedTroopType(baseType, isCharacter, subType, intrinsicArmour);
    }

    public static int GetDefaultFilesWidth(BaseTroopType type) => type switch
    {
        BaseTroopType.Infantry => 5,
        BaseTroopType.Cavalry => 5,
        BaseTroopType.MonstrousInfantry => 3,
        BaseTroopType.MonstrousCavalry => 3,
        BaseTroopType.MonstrousBeast => 3,
        BaseTroopType.Chariot => 3,
        _ => 5
    };
}
