namespace Warhammer.Models.Domain;

/// <summary>
/// A supported game system/edition. Each enabled system ships its own data folder
/// under wwwroot/data/{Key}/ (core rules, faction rosters, magic items, manifest).
/// </summary>
public sealed record GameSystem(string Key, string DisplayName, bool Enabled);

public static class GameSystems
{
    public const string WapV2 = "wap-v2";
    public const string WapV3 = "wap-v3";
    public const string Fantasy8th = "fantasy-8th";
    public const string OldWorld = "old-world";

    public const string Default = WapV2;

    public static readonly IReadOnlyList<GameSystem> All =
    [
        new(WapV2, "WAP v2", Enabled: true),
        new(WapV3, "WAP v3", Enabled: true),
        new(Fantasy8th, "Fantasy 8th", Enabled: false),
        new(OldWorld, "Old World", Enabled: false),
    ];

    public static bool IsEnabled(string? key) =>
        All.Any(s => s.Enabled && s.Key == key);

    /// <summary>How a system key reads to a person, falling back to the key itself.</summary>
    public static string DisplayNameFor(string? key) =>
        All.FirstOrDefault(s => s.Key == key)?.DisplayName ?? key ?? "";
}
