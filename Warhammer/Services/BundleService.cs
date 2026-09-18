using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Services;

/// <summary>
/// Loads the active system's bundle — <c>data/{system}/{system}.json</c>, the whole edition in
/// one file — and hands out its parts. The file is fetched once; factions are deserialised one
/// at a time on first use, since a page needs one book and the bundle holds thirty.
/// </summary>
public class BundleService
{
    private readonly HttpClient _http;
    private readonly GameSystemService _system;
    private BundleData? _bundle;
    private IReadOnlyList<Faction>? _factions;
    private readonly ConcurrentDictionary<string, FactionData> _factionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _bundleLock = new(1, 1);
    private TroopTypesData? _troopTypes;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public BundleService(HttpClient http, GameSystemService system)
    {
        _http = http;
        _system = system;
    }

    /// <summary>Expose the HTTP client for loading additional data files.</summary>
    public HttpClient GetHttpClient() => _http;

    /// <summary>The bundle file's path inside the active system's data folder.</summary>
    public string BundlePath => _system.DataPath($"{_system.CurrentSystem}.json");

    /// <summary>The bundle, fetched on first call and held for the session.</summary>
    public async Task<BundleData> GetBundleAsync()
    {
        if (_bundle != null)
            return _bundle;

        // One fetch, however many callers arrive while it is in flight: the file is large and
        // the test suite shares one service across parallel classes.
        await _bundleLock.WaitAsync();
        try
        {
            if (_bundle != null)
                return _bundle;

            var bundle = await _http.GetFromJsonAsync<BundleData>(BundlePath, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to load {BundlePath}");

            if (bundle.FormatVersion < 2)
                throw new InvalidOperationException(
                    $"{BundlePath} is formatVersion {bundle.FormatVersion}; this build reads formatVersion 2.");

            // The rulebook's common items carry no ids in the export, unlike a faction's own, and
            // the pickers and saved lists address items by id. The name is what an id would be
            // derived from, so derive it here until the export does.
            if (bundle.Rulebook.Items.Any(i => string.IsNullOrEmpty(i.Id)))
            {
                var items = bundle.Rulebook.Items
                    .Select(i => string.IsNullOrEmpty(i.Id) ? i with { Id = FactionUpgrades.Slug(i.Name) } : i)
                    .ToList();
                bundle = bundle with { Rulebook = bundle.Rulebook with { Items = items } };
            }

            _bundle = bundle;
            return _bundle;
        }
        finally
        {
            _bundleLock.Release();
        }
    }

    /// <summary>
    /// Every army book in the bundle, in the bundle's order. Read off each faction's own
    /// header fields without materialising its units.
    /// </summary>
    public async Task<IReadOnlyList<Faction>> GetFactionsAsync()
    {
        if (_factions != null)
            return _factions;

        var bundle = await GetBundleAsync();
        var factions = new List<Faction>(bundle.Factions.Count);
        foreach (var (key, element) in bundle.Factions)
        {
            var name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? key
                : key;
            var align = element.TryGetProperty("align", out var a) && a.ValueKind == JsonValueKind.String
                ? a.GetString()
                : null;
            factions.Add(new Faction(key, name, align));
        }

        _factions = factions;
        return _factions;
    }

    /// <summary>One army book, deserialised on first call and cached.</summary>
    public async Task<FactionData> GetFactionAsync(string factionId)
    {
        if (_factionCache.TryGetValue(factionId, out var cached))
            return cached;

        var bundle = await GetBundleAsync();
        if (!bundle.Factions.TryGetValue(factionId, out var element))
            throw new InvalidOperationException($"The bundle has no faction '{factionId}'.");

        var faction = element.Deserialize<FactionData>(JsonOptions)
            ?? throw new InvalidOperationException($"Faction '{factionId}' could not be read.");

        if (string.IsNullOrEmpty(faction.Id))
            faction = faction with { Id = factionId };

        _factionCache[factionId] = faction;
        return faction;
    }

    /// <summary>Load the active system's troop type figures. Cached after first call.</summary>
    public async Task<TroopTypesData> GetTroopTypesAsync()
    {
        if (_troopTypes != null)
            return _troopTypes;

        _troopTypes = await _http.GetFromJsonAsync<TroopTypesData>(
            _system.DataPath("troop-types.json"), JsonOptions)
            ?? throw new InvalidOperationException("Failed to load troop-types.json");
        return _troopTypes;
    }
}
