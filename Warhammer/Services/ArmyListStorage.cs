namespace Warhammer.Services;

/// <summary>
/// The localStorage keys army lists are held under. Both keys are namespaced per game system,
/// because a list only means anything against the data it was built from.
/// </summary>
public static class ArmyListStorage
{
    /// <summary>The Builder's autosave of the list being edited.</summary>
    public static string ActiveKey(string system) => $"warhammer.armylist.active.{system}";

    /// <summary>
    /// The list handed to the print view. Written on the way to <c>/roster</c> rather than read
    /// from the autosave, because the autosave is fire-and-forget and edits that don't revalidate
    /// — renaming the list, for one — never trigger it.
    /// </summary>
    public static string PrintKey(string system) => $"warhammer.armylist.print.{system}";
}
