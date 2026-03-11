namespace Warhammer.Models.Domain;

/// <summary>
/// Shadow Dances of Loec — chosen at the start of each round of close combat.
/// </summary>
public enum ShadowDance
{
    /// <summary>No dance selected.</summary>
    None,

    /// <summary>Armour Piercing (1) and Killing Blow.</summary>
    WhirlingDeath,

    /// <summary>+1 Attack.</summary>
    StormOfBlades,

    /// <summary>All close combat attacks against the model suffer -1 To Hit.</summary>
    TheShadowsCoil,

    /// <summary>Always Strikes First.</summary>
    WovenMist,
}
