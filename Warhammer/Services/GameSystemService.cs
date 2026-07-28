using Warhammer.Models.Domain;

namespace Warhammer.Services;

/// <summary>
/// Holds the active game system for the browser session. Initialized once at startup
/// from localStorage; switching systems persists the new key and forces a full page
/// reload, so no service ever observes a system change mid-session.
/// </summary>
public class GameSystemService
{
    public const string StorageKey = "warhammer.gameSystem";

    public string CurrentSystem { get; private set; } = GameSystems.Default;

    public void Initialize(string? persistedKey)
    {
        if (GameSystems.IsEnabled(persistedKey))
            CurrentSystem = persistedKey!;
    }

    /// <summary>Resolve a data file path inside the active system's data folder.</summary>
    public string DataPath(string relative) => $"data/{CurrentSystem}/{relative}";
}
