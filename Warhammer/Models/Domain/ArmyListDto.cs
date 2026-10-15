namespace Warhammer.Models.Domain;

/// <summary>
/// Wire format for army list export/import. Decouples the persisted shape
/// from the runtime <see cref="ArmyList"/>, so refactors don't break save files.
/// </summary>
public sealed record ArmyListDto
{
    public const string CurrentSchema = "wap-army-list/v1";

    public string Schema { get; init; } = CurrentSchema;
    // Additive field: lists saved before multi-system support default to wap-v2.
    public string System { get; init; } = GameSystems.Default;
    public string Faction { get; init; } = "";
    public int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<ArmyListUnitDto> Units { get; init; } = new();

    // Additive field: lists saved without house-rule overrides get the system's percentages.
    // Omitted from the written JSON when nothing is overridden.
    public CompositionOverrides? Composition { get; init; }
}

public sealed record ArmyListUnitDto
{
    public string UnitName { get; init; } = "";
    public string? CustomName { get; init; }
    public int ModelCount { get; init; } = 1;
    public int Frontage { get; init; } = 1;
    public string? SelectedWeapon { get; init; }
    public bool HasChampion { get; init; }
    public bool HasMusician { get; init; }
    public bool HasStandardBearer { get; init; }
    public Dictionary<int, int> SelectedUpgrades { get; init; } = new();

    // Additive field: selections in "choose any" option groups, several per group.
    // Omitted from the written JSON when no such group has a selection.
    public Dictionary<int, List<int>>? SelectedMultiUpgrades { get; init; }

    // Additive field: sub-options the command models take (a Leader's long rifle).
    // Omitted from the written JSON when none is picked.
    public List<CommandUpgradeDto>? CommandUpgrades { get; init; }
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

/// <summary>Wire form of a <see cref="CommandUpgradePick"/>: indices into the unit's options as the book lists them.</summary>
public sealed record CommandUpgradeDto
{
    public int OptionIndex { get; init; }
    public int SubOptionIndex { get; init; }
    public int ChoiceIndex { get; init; }
}
