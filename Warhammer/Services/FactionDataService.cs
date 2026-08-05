using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class FactionDataService
{
    private readonly HttpClient _http;
    private readonly GameSystemService _system;
    private readonly Dictionary<string, FactionData> _unitCache = new();
    private readonly Dictionary<string, FactionExtrasData> _extrasCache = new();
    private SystemManifest? _manifest;
    private CompositionData? _composition;
    private TroopTypesData? _troopTypes;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FactionDataService(HttpClient http, GameSystemService system)
    {
        _http = http;
        _system = system;
    }

    /// <summary>Expose the HTTP client for loading additional data files.</summary>
    public HttpClient GetHttpClient() => _http;

    /// <summary>Load the active system's manifest (faction list). Cached after first call.</summary>
    public async Task<SystemManifest> GetManifestAsync()
    {
        if (_manifest != null)
            return _manifest;

        _manifest = await _http.GetFromJsonAsync<SystemManifest>(
            _system.DataPath("manifest.json"), JsonOptions)
            ?? throw new InvalidOperationException("Failed to load manifest.json");
        return _manifest;
    }

    public async Task<FactionData> GetFactionUnitsAsync(string factionKey)
    {
        if (_unitCache.TryGetValue(factionKey, out var cached))
            return cached;

        var data = await _http.GetFromJsonAsync<FactionData>(
            _system.DataPath($"factions/{factionKey}.json"), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load {factionKey}.json");

        NormalizeMountCategories(data);
        _unitCache[factionKey] = data;
        return data;
    }

    private static void NormalizeMountCategories(FactionData data)
    {
        foreach (var unit in data.Units)
        {
            // Already marked as CHARACTER MOUNTS (lizardmen, the-empire) — normalize casing
            if (string.Equals(unit.Category, "CHARACTER MOUNTS", StringComparison.OrdinalIgnoreCase))
            {
                unit.Category = "Character Mounts";
                continue;
            }

            // Detect mount entries: no points cost, no unit size, but has a stat profile
            if (unit.PointsCost == null && unit.UnitSize == null && unit.Profiles.Count > 0)
            {
                unit.Category = "Character Mounts";
            }
        }
    }

    /// <summary>Load the active system's composition percentages. Cached after first call.</summary>
    public async Task<CompositionData> GetCompositionAsync()
    {
        if (_composition != null)
            return _composition;

        _composition = await _http.GetFromJsonAsync<CompositionData>(
            _system.DataPath("composition.json"), JsonOptions)
            ?? throw new InvalidOperationException("Failed to load composition.json");
        return _composition;
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

    public async Task<FactionExtrasData> GetFactionExtrasAsync(string factionKey)
    {
        if (_extrasCache.TryGetValue(factionKey, out var cached))
            return cached;

        var data = await _http.GetFromJsonAsync<FactionExtrasData>(
            _system.DataPath($"factions/{factionKey}-extras.json"), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load {factionKey}-extras.json");

        _extrasCache[factionKey] = data;
        return data;
    }
}
