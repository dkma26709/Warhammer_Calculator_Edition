using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class CoreRulesService
{
    private readonly HttpClient _http;
    private readonly GameSystemService _system;
    private CoreRulesData? _data;
    private bool _loaded;

    // Charts are read inside the simulation's innermost loops, so cells are parsed once at
    // load rather than on every lookup.
    private RollRequirement[][]? _toHit;
    private RollRequirement[][]? _toWound;

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
        var data = await _http.GetFromJsonAsync<CoreRulesData>(_system.DataPath("core-rules.json"), JsonOptions)
            ?? throw new InvalidOperationException("Failed to load core-rules.json");
        Initialize(data);
    }

    /// <summary>
    /// Adopt an already-deserialized rules set, resolving its charts. Separate from
    /// <see cref="EnsureLoadedAsync"/> so callers holding the JSON can skip the HTTP fetch.
    /// </summary>
    public void Initialize(CoreRulesData data)
    {
        _data = data;
        // No published to-hit chart uses a confirming second roll, and the resolver relies on
        // that, so a compound cell there is rejected rather than silently half-applied.
        _toHit = ResolveChart(data.CloseCombatToHit, "closeCombatToHit", allowCompound: false);
        _toWound = ResolveChart(data.ToWound, "toWound", allowCompound: true);
        _loaded = true;
    }

    /// <summary>
    /// Parse a chart's cells up front, refusing a chart the engine cannot read rather than
    /// letting a bad cell surface as a wrong result deep in a simulation.
    /// </summary>
    private RollRequirement[][] ResolveChart(CombatTable chart, string label, bool allowCompound)
    {
        var table = chart.Table;
        if (table.Length == 0)
            throw new InvalidOperationException(
                $"core-rules.json for '{_system.CurrentSystem}' has no {label}.table.");

        var resolved = new RollRequirement[table.Length][];
        for (int row = 0; row < table.Length; row++)
        {
            var cells = table[row];
            resolved[row] = new RollRequirement[cells.Length];
            for (int col = 0; col < cells.Length; col++)
            {
                if (!RollRequirement.TryParse(cells[col], out var requirement))
                    throw new InvalidOperationException(
                        $"core-rules.json for '{_system.CurrentSystem}': {label}.table" +
                        $"[{row + 1}][{col + 1}] is '{cells[col]}', which is not a chart score.");

                if (!allowCompound && requirement.NeedsSecondRoll)
                    throw new InvalidOperationException(
                        $"core-rules.json for '{_system.CurrentSystem}': {label}.table" +
                        $"[{row + 1}][{col + 1}] is '{cells[col]}', but {label} does not support " +
                        "a confirming second roll.");

                resolved[row][col] = requirement;
            }
        }
        return resolved;
    }

    private RollRequirement Lookup(RollRequirement[][]? table, int rowValue, int colValue, string label)
    {
        if (table is null)
            throw new InvalidOperationException($"{label} not loaded. Call EnsureLoadedAsync first.");

        int row = Math.Clamp(rowValue, 1, table.Length) - 1;
        int col = Math.Clamp(colValue, 1, table[row].Length) - 1;
        return table[row][col];
    }

    /// <summary>What a close combat attack must roll to hit. WS values clamped to 1-10.</summary>
    public RollRequirement GetToHitRequirement(int attackerWS, int defenderWS) =>
        Lookup(_toHit, attackerWS, defenderWS, "closeCombatToHit");

    /// <summary>What a hit must roll to wound. S and T values clamped to 1-10.</summary>
    public RollRequirement GetToWoundRequirement(int strength, int toughness) =>
        Lookup(_toWound, strength, toughness, "toWound");

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
