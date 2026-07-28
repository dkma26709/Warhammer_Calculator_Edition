using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class CoreRulesService
{
    private readonly HttpClient _http;
    private readonly GameSystemService _system;
    private CoreRulesData? _data;
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CoreRulesService(HttpClient http, GameSystemService system)
    {
        _http = http;
        _system = system;
    }

    public CoreRulesData Data => _data ?? throw new InvalidOperationException("Core rules not loaded. Call EnsureLoadedAsync first.");

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _data = await _http.GetFromJsonAsync<CoreRulesData>(_system.DataPath("core-rules.json"), JsonOptions)
            ?? throw new InvalidOperationException("Failed to load core-rules.json");
        _loaded = true;
    }

    /// <summary>
    /// Look up the D6 score needed to hit in close combat.
    /// WS values clamped to 1-10.
    /// </summary>
    public int GetToHitScore(int attackerWS, int defenderWS)
    {
        var table = Data.CloseCombatToHit.Table;
        int row = Math.Clamp(attackerWS, 1, table.Length) - 1;
        int col = Math.Clamp(defenderWS, 1, table[0].Length) - 1;
        return table[row][col];
    }

    /// <summary>
    /// Look up the D6 score needed to wound.
    /// S and T values clamped to 1-10.
    /// </summary>
    public int GetToWoundScore(int strength, int toughness)
    {
        var table = Data.ToWound.Table;
        int row = Math.Clamp(strength, 1, table.Length) - 1;
        int col = Math.Clamp(toughness, 1, table[0].Length) - 1;
        return table[row][col];
    }

    /// <summary>
    /// Get the armour save modifier for a given strength.
    /// Returns a negative value (e.g., -1 for S4, -2 for S5).
    /// </summary>
    public int GetArmourSaveModifier(int strength)
    {
        var key = $"S{Math.Clamp(strength, 1, 10)}";
        return Data.ArmourSaves.StrengthModifiers.TryGetValue(key, out var mod) ? mod : 0;
    }

}
