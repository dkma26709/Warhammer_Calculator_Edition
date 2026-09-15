namespace Warhammer.Models.Domain;

/// <summary>
/// Army-builder state for a unit entry: identity, army roles, and magic/wizard
/// selections made at list-building time. Lives on <see cref="UnitConfiguration.Loadout"/>
/// and is never read by the combat engine — simulation-relevant choices
/// (weapons, upgrades, modifiers) stay on <see cref="UnitConfiguration"/> itself.
/// </summary>
public sealed class ArmyLoadout
{
    /// <summary>
    /// Optional user-supplied name (e.g. "Aldric the Bold" on an Empire Captain).
    /// Displayed alongside the unit's book name, not as a replacement.
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>Wizard level (1-4) for casting spells. Only meaningful for wizard-capable units.</summary>
    public int WizardLevel { get; set; } = 1;

    /// <summary>Selected lore name (matches a lore's name in the bundle). Null if not a wizard or unset.</summary>
    public string? SelectedLore { get; set; }

    /// <summary>Names of spells the wizard knows (picked from the selected lore at army creation time).</summary>
    public List<string> SelectedSpells { get; } = [];

    /// <summary>Magic standard (banner) selected by the unit. Null when none chosen.</summary>
    public SelectedMagicItem? SelectedBanner { get; set; }

    /// <summary>True when this character is designated as the army's General.</summary>
    public bool IsGeneral { get; set; }

    /// <summary>True when this character is the army's Battle Standard Bearer.</summary>
    public bool IsBattleStandardBearer { get; set; }

    /// <summary>
    /// Faction-specific upgrade selections keyed by family — the faction's upgrade chapter in
    /// kebab-case: "knightly-orders", "blessed-spawnings", "runic-items". Values are the chosen
    /// entries of that family.
    /// </summary>
    public Dictionary<string, List<SelectedFactionUpgrade>> SelectedFactionUpgrades { get; } = new();
}
