namespace Warhammer.Models.Domain;

/// <summary>
/// A supported game system/edition. Each enabled system ships its own data folder under
/// wwwroot/data/{Key}/: the bundle {Key}.json with every army book, plus the hand-maintained
/// core-rules.json and troop-types.json the combat engine reads.
/// </summary>
public sealed record GameSystem(string Key, string DisplayName, bool Enabled);

public static class GameSystems
{
    public const string War = "war";

    public const string Default = War;

    public static readonly IReadOnlyList<GameSystem> All =
    [
        new(War, "Warhammer Armies Revamped", Enabled: true),
    ];

    public static bool IsEnabled(string? key) =>
        All.Any(s => s.Enabled && s.Key == key);

    /// <summary>How a system key reads to a person, falling back to the key itself.</summary>
    public static string DisplayNameFor(string? key) =>
        All.FirstOrDefault(s => s.Key == key)?.DisplayName ?? key ?? "";
}
