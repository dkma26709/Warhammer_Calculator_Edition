namespace Warhammer.Models.Json;

/// <summary>
/// Per-system manifest (data/{system}/manifest.json) listing the factions
/// available in that system. Faction keys must match FactionKey JSON keys.
/// </summary>
public class SystemManifest
{
    public string System { get; set; } = "";
    public List<string> Factions { get; set; } = new();
}
