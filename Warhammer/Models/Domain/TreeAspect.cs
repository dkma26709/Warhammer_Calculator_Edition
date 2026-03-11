namespace Warhammer.Models.Domain;

/// <summary>
/// Tree Aspects — chosen at the start of each round of close combat.
/// </summary>
public enum TreeAspect
{
    /// <summary>No aspect selected.</summary>
    None,

    /// <summary>+1 Attack.</summary>
    Birch,

    /// <summary>+1 Toughness.</summary>
    Oak,

    /// <summary>All close combat attacks against the model suffer -1 To Hit.</summary>
    Willow,
}
