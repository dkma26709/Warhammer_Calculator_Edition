using System.Net.Http.Json;
using Warhammer.Models;

namespace Warhammer.Services;

public class FactionDataService
{
    private readonly HttpClient _httpClient;
    private List<FactionIndex>? _factionIndex;
    private readonly Dictionary<string, ExtractedFactionData> _factionCache = new();

    public FactionDataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FactionIndex>> GetFactionsAsync()
    {
        _factionIndex ??= await _httpClient.GetFromJsonAsync<List<FactionIndex>>("data/factions-full.json");
        return _factionIndex ?? new List<FactionIndex>();
    }

    public async Task<ExtractedFactionData?> GetFactionDataAsync(string factionId)
    {
        if (_factionCache.TryGetValue(factionId, out var cached))
            return cached;

        var factions = await GetFactionsAsync();
        var factionInfo = factions.FirstOrDefault(f => f.Id == factionId);
        if (factionInfo is null) return null;

        var data = await _httpClient.GetFromJsonAsync<ExtractedFactionData>(factionInfo.File);
        if (data is not null)
            _factionCache[factionId] = data;

        return data;
    }
}
