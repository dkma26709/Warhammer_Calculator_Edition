using System.Net.Http.Json;
using System.Text.Json;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public class CoreRulesService
{
    private readonly HttpClient _http;
    private CoreRulesData? _data;
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CoreRulesService(HttpClient http)
    {
        _http = http;
    }

    public CoreRulesData Data => _data ?? throw new InvalidOperationException("Core rules not loaded. Call EnsureLoadedAsync first.");

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _data = await _http.GetFromJsonAsync<CoreRulesData>("data/core-rules.json", JsonOptions)
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

    public WeaponEntry? GetCloseCombatWeapon(string key)
    {
        return Data.Weapons.CloseCombat.TryGetValue(key, out var w) ? w : null;
    }

    public WeaponEntry? GetMissileWeapon(string key)
    {
        return Data.Weapons.Missile.TryGetValue(key, out var w) ? w : null;
    }

    public WeaponEntry GetHandWeapon()
    {
        return Data.Weapons.CloseCombat["handWeapon"];
    }

    public ArmourEntry? GetArmour(string key)
    {
        return Data.Armour.TryGetValue(key, out var a) ? a : null;
    }

    public SpecialRuleEntry? GetSpecialRule(string key)
    {
        return Data.CommonSpecialRules.TryGetValue(key, out var r) ? r : null;
    }

    public IReadOnlyDictionary<string, SpecialRuleEntry> AllSpecialRules =>
        Data.CommonSpecialRules;

    public IReadOnlyDictionary<string, WeaponEntry> AllCloseCombatWeapons =>
        Data.Weapons.CloseCombat;

    public IReadOnlyDictionary<string, ArmourEntry> AllArmour =>
        Data.Armour;

    /// <summary>
    /// Parse weapon strength field: "As user" -> userS, "+1" -> userS+1, "3" -> 3
    /// </summary>
    public static int ParseWeaponStrength(string strengthField, int userStrength)
    {
        if (string.Equals(strengthField, "As user", StringComparison.OrdinalIgnoreCase))
            return userStrength;
        if (strengthField.StartsWith('+') && int.TryParse(strengthField, out var mod))
            return userStrength + mod;
        if (int.TryParse(strengthField, out var fixedVal))
            return fixedVal;
        return userStrength;
    }

    /// <summary>
    /// Parse armour save field "+N/M+" into (modifier, standaloneSave).
    /// E.g., "+3/4+" -> (3, 4)
    /// </summary>
    public static (int Modifier, int StandaloneSave) ParseArmourSave(string saveField)
    {
        var match = System.Text.RegularExpressions.Regex.Match(saveField, @"\+(\d)/(\d)\+");
        if (match.Success)
        {
            int modifier = int.Parse(match.Groups[1].Value);
            int standalone = int.Parse(match.Groups[2].Value);
            return (modifier, standalone);
        }
        return (0, 7);
    }
}
