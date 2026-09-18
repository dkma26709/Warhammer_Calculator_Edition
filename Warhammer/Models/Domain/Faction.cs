namespace Warhammer.Models.Domain;

/// <summary>
/// An army book in the active system. <see cref="Id"/> is the bundle's slug — "empire",
/// "orcs-goblins" — and is the key everywhere: unit lookups, saved lists, portraits, the race
/// table the engine reads for Hatred.
/// </summary>
public sealed record Faction(string Id, string Name, string? Align);
