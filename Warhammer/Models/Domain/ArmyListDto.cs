namespace Warhammer.Models.Domain;

/// <summary>
/// Wire format for army list export/import. Decouples the persisted shape from the runtime
/// <see cref="ArmyList"/>, so refactors don't break save files. Units, options and items are
/// addressed by the bundle's stable ids, so a name corrected in a later release still resolves.
/// </summary>
public sealed record ArmyListDto
{
    public const string CurrentSchema = "army-list/v2";

    public string Schema { get; init; } = CurrentSchema;
    public string System { get; init; } = GameSystems.Default;

    /// <summary>The faction's bundle id.</summary>
    public string Faction { get; init; } = "";

    public int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<ArmyListUnitDto> Units { get; init; } = new();

    // Omitted from the written JSON when nothing is overridden.
    public CompositionOverrides? Composition { get; init; }
}

public sealed record ArmyListUnitDto
{
    /// <summary>The entry's bundle id — the key a saved unit resolves by.</summary>
    public string UnitId { get; init; } = "";

    /// <summary>The book name at the time of saving, for a reader and as a fallback when the id is unknown.</summary>
    public string UnitName { get; init; } = "";

    /// <summary>Which of the entry's priced profiles is fielded. Null means the first.</summary>
    public string? ProfileId { get; init; }

    public string? CustomName { get; init; }
    public int ModelCount { get; init; } = 1;
    public int Frontage { get; init; } = 1;
    public string? SelectedWeapon { get; init; }
    public bool HasChampion { get; init; }
    public bool HasMusician { get; init; }
    public bool HasStandardBearer { get; init; }

    /// <summary>Option selections: group id → choice id → count.</summary>
    public Dictionary<string, Dictionary<string, int>> Selections { get; init; } = new();

    public List<MagicItemDto> MagicItems { get; init; } = new();
    public MagicItemDto? Banner { get; init; }
    public int WizardLevel { get; init; } = 1;
    public string? SelectedLore { get; init; }
    public List<string> SelectedSpells { get; init; } = new();
    public List<UnitModifier> Modifiers { get; init; } = new();
    public string ShadowDance { get; init; } = "None";
    public string TreeAspect { get; init; } = "None";
    public bool UseTreeWhack { get; init; }
    public bool IsGeneral { get; init; }
    public bool IsBattleStandardBearer { get; init; }

    /// <summary>Faction upgrade picks by family: the entry ids taken. Costs are re-resolved on import.</summary>
    public Dictionary<string, List<string>> FactionUpgrades { get; init; } = new();
}

public sealed record MagicItemDto
{
    public string? Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public double PointsCost { get; init; }
    public UnitModifier Modifier { get; init; } = new();
}
