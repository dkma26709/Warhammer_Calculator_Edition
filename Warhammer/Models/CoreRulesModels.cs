namespace Warhammer.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

public class CoreRulesJson
{
    [JsonPropertyName("closeCombatToHit")]
    public CoreTableSection? CloseCombatToHit { get; set; }

    [JsonPropertyName("toWound")]
    public CoreTableSection? ToWound { get; set; }

    [JsonPropertyName("armourSaves")]
    public ArmourSavesSection? ArmourSaves { get; set; }

    [JsonPropertyName("weapons")]
    public WeaponsSection? Weapons { get; set; }

    [JsonPropertyName("armour")]
    public Dictionary<string, CoreArmourJson>? Armour { get; set; }

    [JsonPropertyName("combatResult")]
    public JsonElement? CombatResult { get; set; }

    [JsonPropertyName("commonSpecialRules")]
    public Dictionary<string, string>? CommonSpecialRules { get; set; }
}

public class CoreTableSection
{
    [JsonPropertyName("table")]
    public int[][]? Table { get; set; }
}

public class ArmourSavesSection
{
    [JsonPropertyName("strengthModifiers")]
    public Dictionary<string, int>? StrengthModifiers { get; set; }
}

public class WeaponsSection
{
    [JsonPropertyName("closeCombat")]
    public Dictionary<string, CoreWeaponJson>? CloseCombat { get; set; }
}

public class CoreWeaponJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("strength")]
    public string? Strength { get; set; }

    [JsonPropertyName("specialRules")]
    public List<string> SpecialRules { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class CoreArmourJson
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("combatSave")]
    public string? CombatSave { get; set; }

    [JsonPropertyName("missileSave")]
    public string? MissileSave { get; set; }

    [JsonPropertyName("specialRules")]
    public List<string>? SpecialRules { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
