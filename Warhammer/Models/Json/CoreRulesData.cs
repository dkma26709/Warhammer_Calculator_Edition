using System.Text.Json.Serialization;

namespace Warhammer.Models.Json;

public record CoreRulesData
{
    public string Name { get; init; } = "";
    public CombatTable CloseCombatToHit { get; init; } = new();
    public CombatTable ToWound { get; init; } = new();
    public ShootingTable ShootingToHit { get; init; } = new();
    public ArmourSaveData ArmourSaves { get; init; } = new();
    public WeaponsData Weapons { get; init; } = new();
    public Dictionary<string, ArmourEntry> Armour { get; init; } = new();
    public Dictionary<string, SpecialRuleEntry> CommonSpecialRules { get; init; } = new();
    public Dictionary<string, TroopTypeEntry> TroopTypes { get; init; } = new();
}

public record CombatTable
{
    public string Description { get; init; } = "";

    /// <summary>
    /// Chart cells verbatim from the book — "4+", "6+/3+", "n/a". Strings rather than ints
    /// because wap-v3's to-wound chart carries compound results and immunity; see
    /// <see cref="Warhammer.Models.Domain.RollRequirement"/>. Resolved once at load.
    /// </summary>
    [JsonConverter(typeof(ChartCellRowsConverter))]
    public string[][] Table { get; init; } = [];

    public TableAxes TableAxes { get; init; } = new();
}

public record TableAxes
{
    public string Rows { get; init; } = "";
    public string Columns { get; init; } = "";
}

public record ShootingTable
{
    public string Description { get; init; } = "";
    public Dictionary<string, int> Table { get; init; } = new();
    public Dictionary<string, string> Modifiers { get; init; } = new();
}

public record ArmourSaveData
{
    public string Description { get; init; } = "";
    public Dictionary<string, int> StrengthModifiers { get; init; } = new();
}

public record WeaponsData
{
    public Dictionary<string, WeaponEntry> CloseCombat { get; init; } = new();
    public Dictionary<string, WeaponEntry> Missile { get; init; } = new();
}

public record WeaponEntry
{
    public string Name { get; init; } = "";
    public string Range { get; init; } = "";
    public string Strength { get; init; } = "As user";
    public string? SpecialRules { get; init; }
    public string? Notes { get; init; }
}

public record ArmourEntry
{
    public string Name { get; init; } = "";
    public string CombatSave { get; init; } = "";
    public string MissileSave { get; init; } = "";
    public string? SpecialRules { get; init; }
    public string? Notes { get; init; }
}

public record SpecialRuleEntry
{
    public string Name { get; init; } = "";
    public bool Parameterized { get; init; }
    public string Description { get; init; } = "";
}

public record TroopTypeEntry
{
    public string Name { get; init; } = "";
    public bool Parameterized { get; init; }
    public string Description { get; init; } = "";
}
