namespace Warhammer.Models.Domain;

/// <summary>
/// Wire format for army list export/import. Decouples the persisted shape
/// from the runtime <see cref="ArmyList"/>, so refactors don't break save files.
/// </summary>
public sealed record ArmyListDto
{
    public const string CurrentSchema = "wap-army-list/v1";

    public string Schema { get; init; } = CurrentSchema;
    public string Faction { get; init; } = "";
    public int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<ArmyListUnitDto> Units { get; init; } = new();
}

public sealed record ArmyListUnitDto
{
    public string UnitName { get; init; } = "";
    public int ModelCount { get; init; } = 1;
    public int Frontage { get; init; } = 1;
    public string? SelectedWeapon { get; init; }
    public bool HasChampion { get; init; }
    public bool HasMusician { get; init; }
    public bool HasStandardBearer { get; init; }
    public Dictionary<int, int> SelectedUpgrades { get; init; } = new();
    public List<MagicItemDto> MagicItems { get; init; } = new();
    public MagicItemDto? Banner { get; init; }
    public int WizardLevel { get; init; } = 2;
    public string? SelectedLore { get; init; }
    public List<string> SelectedSpells { get; init; } = new();
    public List<UnitModifier> Modifiers { get; init; } = new();
    public string ShadowDance { get; init; } = "None";
    public string TreeAspect { get; init; } = "None";
    public bool UseTreeWhack { get; init; }
    public bool IsGeneral { get; init; }
    public bool IsBattleStandardBearer { get; init; }
    public Dictionary<string, List<string>> SelectedFactionUpgrades { get; init; } = new();
}

public sealed record MagicItemDto
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public double PointsCost { get; init; }
    public UnitModifier Modifier { get; init; } = new();
}
