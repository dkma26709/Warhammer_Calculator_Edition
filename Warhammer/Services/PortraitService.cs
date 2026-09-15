using System.Net.Http.Json;
using System.Text;

namespace Warhammer.Services;

/// <summary>
/// Resolves unit portrait URLs against a pre-generated manifest so the app
/// can avoid 404s and render fallbacks cleanly when a portrait is missing.
/// The manifest is produced by the <c>tools/PortraitFetcher</c> utility and is keyed by the
/// faction's bundle id.
/// </summary>
public sealed class PortraitService
{
    private readonly HttpClient _http;
    private Dictionary<string, HashSet<string>> _slugsByFaction = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public PortraitService(HttpClient http) => _http = http;

    /// <summary>
    /// Load the manifest from <c>/data/portrait-manifest.json</c>. Safe to call repeatedly.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            var manifest = await _http.GetFromJsonAsync<Dictionary<string, List<string>>>("data/portrait-manifest.json");
            if (manifest != null)
            {
                _slugsByFaction = manifest.ToDictionary(
                    kv => kv.Key,
                    kv => new HashSet<string>(kv.Value, StringComparer.Ordinal),
                    StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Manifest missing or malformed — behave as "no portraits available".
        }
        _loaded = true;
    }

    /// <summary>True when a portrait exists for this faction + unit name.</summary>
    public bool HasPortrait(string factionId, string unitName)
    {
        var slug = Slugify(unitName);
        return _slugsByFaction.TryGetValue(factionId, out var slugs)
            && slugs.Contains(slug);
    }

    /// <summary>Relative URL to the portrait asset. Does not check for existence.</summary>
    public string GetPortraitUrl(string factionId, string unitName) =>
        $"images/units/{factionId}/{Slugify(unitName)}.png";

    /// <summary>Normalize a unit name into the same slug the fetcher uses for filenames.</summary>
    internal static string Slugify(string s)
    {
        var sb = new StringBuilder(s.Length);
        char last = '-';
        foreach (var c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(c); last = c; }
            else if (last != '-') { sb.Append('-'); last = '-'; }
        }
        return sb.ToString().Trim('-');
    }
}
