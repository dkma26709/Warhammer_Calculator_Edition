namespace Warhammer.Services;

/// <summary>
/// The localStorage keys army lists are held under. Both keys are namespaced per game system,
/// because a list only means anything against the data it was built from.
/// </summary>
public static class ArmyListStorage
{
    /// <summary>The Builder's autosave from before multi-system support, which carried no suffix.</summary>
    public const string LegacyActiveKey = "warhammer.armylist.active";

    /// <summary>The Builder's autosave of the list being edited.</summary>
    public static string ActiveKey(string system) => $"{LegacyActiveKey}.{system}";

    /// <summary>
    /// The list handed to the print view. Written on the way to <c>/roster</c> rather than read
    /// from the autosave, because the autosave is fire-and-forget and edits that don't revalidate
    /// — renaming the list, for one — never trigger it.
    /// </summary>
    public static string PrintKey(string system) => $"warhammer.armylist.print.{system}";

    /// <summary>
    /// An autosave the Builder could not open (its army left the system, say), moved out of
    /// <see cref="ActiveKey"/> so the next edit does not overwrite it. The Builder offers it
    /// again once it opens. One per system: a later list that will not open takes its place.
    /// </summary>
    public static string ShelvedKey(string system) => $"warhammer.armylist.shelved.{system}";
}
