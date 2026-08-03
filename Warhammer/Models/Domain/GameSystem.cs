namespace Warhammer.Models.Domain;

/// <summary>
/// A supported game system/edition. Each enabled system ships its own data folder
/// under wwwroot/data/{Key}/ (core rules, faction rosters, magic items, manifest).
/// </summary>
public sealed record GameSystem(string Key, string DisplayName, bool Enabled);

public static class GameSystems
{
    public const string Default = "wap-v2";

    public static readonly IReadOnlyList<GameSystem> All =
    [
        new("wap-v2", "WAP v2", Enabled: true),
        new("wap-v3", "WAP v3", Enabled: false),
        new("fantasy-8th", "Fantasy 8th", Enabled: false),
        new("old-world", "Old World", Enabled: false),
    ];

    public static bool IsEnabled(string? key) =>
        All.Any(s => s.Enabled && s.Key == key);
}
