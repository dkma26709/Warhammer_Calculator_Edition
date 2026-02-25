namespace Warhammer.Services;

using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Warhammer.Models;

public class CoreRulesService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Dictionary<string, WeaponDefinition> _weapons = new();
    private Dictionary<string, ArmourDefinition> _armour = new();
    private int[][] _toHitTable = [];
    private int[][] _toWoundTable = [];
    private Dictionary<int, int> _armourSavePenalties = new();
    private bool _loaded;

    private static readonly WeaponDefinition DefaultHandWeapon = new()
    {
        Key = "handWeapon", Name = "Hand Weapon", IsCloseCombat = true
    };

    public CoreRulesService(HttpClient http)
    {
        _http = http;
    }

    public bool IsLoaded => _loaded;
    public IReadOnlyDictionary<string, WeaponDefinition> Weapons => _weapons;
    public IReadOnlyDictionary<string, ArmourDefinition> Armour => _armour;

    /// <summary>
    /// Null-safe hand weapon reference. Falls back to a default if not loaded from JSON.
    /// </summary>
    public WeaponDefinition HandWeapon =>
        _weapons.GetValueOrDefault("handWeapon") ?? DefaultHandWeapon;

    public async Task LoadAsync()
    {
        if (_loaded) return;

        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return; // double-check after acquiring lock

            var json = await _http.GetFromJsonAsync<CoreRulesJson>("data/extracted/core-rules.json");
            if (json == null) throw new InvalidOperationException("Failed to load core-rules.json");

            // Load tables
            _toHitTable = json.CloseCombatToHit?.Table ?? [];
            _toWoundTable = json.ToWound?.Table ?? [];

            // Load armour save penalties (stored as positive values: S4 → 1, S5 → 2, etc.)
            if (json.ArmourSaves?.StrengthModifiers != null)
            {
                foreach (var kvp in json.ArmourSaves.StrengthModifiers)
                {
                    if (int.TryParse(kvp.Key.Replace("S", ""), out int s))
                        _armourSavePenalties[s] = Math.Abs(kvp.Value);
                }
            }

            // Parse weapons
            if (json.Weapons?.CloseCombat != null)
            {
                foreach (var kvp in json.Weapons.CloseCombat)
                    _weapons[kvp.Key] = ParseWeapon(kvp.Key, kvp.Value);
            }

            // Parse armour
            if (json.Armour != null)
            {
                foreach (var kvp in json.Armour)
                    _armour[kvp.Key] = ParseArmour(kvp.Key, kvp.Value);
            }

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    #region Weapon parsing

    private static WeaponDefinition ParseWeapon(string key, CoreWeaponJson data)
    {
        var weapon = new WeaponDefinition
        {
            Key = key,
            Name = data.Name,
            IsCloseCombat = true,
        };

        // Parse strength
        ParseStrength(data.Strength, weapon);

        // Parse special rules
        foreach (var rule in data.SpecialRules)
            ParseWeaponSpecialRule(rule, weapon);

        return weapon;
    }

    private static void ParseStrength(string? strength, WeaponDefinition weapon)
    {
        if (string.IsNullOrEmpty(strength) || strength == "As user")
            return;

        // "+2 first round, +1 subsequent"
        var roundMatch = Regex.Match(strength, @"\+(\d+)\s+first\s+round,?\s*\+(\d+)\s+subsequent");
        if (roundMatch.Success)
        {
            weapon.HasRoundDependentStrength = true;
            weapon.StrengthBonusFirstRound = int.Parse(roundMatch.Groups[1].Value);
            weapon.StrengthBonusSubsequent = int.Parse(roundMatch.Groups[2].Value);
            return;
        }

        // "+1 first round"
        var firstOnlyMatch = Regex.Match(strength, @"\+(\d+)\s+first\s+round");
        if (firstOnlyMatch.Success)
        {
            weapon.HasRoundDependentStrength = true;
            weapon.StrengthBonusFirstRound = int.Parse(firstOnlyMatch.Groups[1].Value);
            weapon.StrengthBonusSubsequent = 0;
            return;
        }

        // "+1" or "+2"
        var simpleMatch = Regex.Match(strength, @"\+(\d+)");
        if (simpleMatch.Success)
        {
            weapon.StrengthBonus = int.Parse(simpleMatch.Groups[1].Value);
        }
    }

    private static void ParseWeaponSpecialRule(string rule, WeaponDefinition weapon)
    {
        var trimmed = rule.Trim();

        // "+1 Attack"
        var attackMatch = Regex.Match(trimmed, @"^\+(\d+)\s+Attack", RegexOptions.IgnoreCase);
        if (attackMatch.Success)
        {
            weapon.AttackBonus = int.Parse(attackMatch.Groups[1].Value);
            return;
        }

        // "Armour Piercing (X)"
        var apMatch = Regex.Match(trimmed, @"Armour Piercing\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (apMatch.Success)
        {
            weapon.ArmourPiercing = int.Parse(apMatch.Groups[1].Value);
            return;
        }

        // "Always Strikes Last"
        if (trimmed.Equals("Always Strikes Last", StringComparison.OrdinalIgnoreCase))
        {
            weapon.AlwaysStrikesLast = true;
            return;
        }

        // "Requires Two Hands"
        if (trimmed.Equals("Requires Two Hands", StringComparison.OrdinalIgnoreCase))
        {
            weapon.RequiresTwoHands = true;
            return;
        }

        // "Fight in Extra Ranks (X)"
        var rankMatch = Regex.Match(trimmed, @"Fight in Extra Ranks?\s*\((\d+)\)", RegexOptions.IgnoreCase);
        if (rankMatch.Success)
        {
            weapon.FightInExtraRanks = int.Parse(rankMatch.Groups[1].Value);
            return;
        }

        // "Parry (6+)" or similar
        if (trimmed.StartsWith("Parry", StringComparison.OrdinalIgnoreCase))
        {
            weapon.HasParry = true;
            return;
        }

        // "+N Initiative on charge" / "+N Initiative when charged to front" / "+N Initiative"
        var initMatch = Regex.Match(trimmed, @"^\+(\d+)\s+Initiative(?:\s+(.+))?", RegexOptions.IgnoreCase);
        if (initMatch.Success)
        {
            weapon.InitiativeModifier = int.Parse(initMatch.Groups[1].Value);
            var condition = initMatch.Groups[2].Value.Trim().ToLowerInvariant();
            if (condition.Contains("on charge"))
                weapon.InitOnCharge = true;
            else if (condition.Contains("when charged"))
                weapon.InitWhenCharged = true;
            return;
        }

        // "Mighty Blow (X) on charge" or "Mighty Blow (X) when charged to front"
        var mbMatch = Regex.Match(trimmed, @"Mighty Blow\s*\((\d+)\)(?:\s+(.+))?", RegexOptions.IgnoreCase);
        if (mbMatch.Success)
        {
            weapon.MightyBlow = int.Parse(mbMatch.Groups[1].Value);
            var condition = mbMatch.Groups[2].Value.Trim().ToLowerInvariant();
            if (condition.Contains("on charge"))
                weapon.MightyBlowOnCharge = true;
            else if (condition.Contains("when charged"))
                weapon.MightyBlowWhenCharged = true;
        }
    }

    #endregion

    #region Armour parsing

    private static ArmourDefinition ParseArmour(string key, CoreArmourJson data)
    {
        var armour = new ArmourDefinition
        {
            Key = key,
            Name = data.Name,
            IsShield = key == "shield",
            IsBuckler = key == "buckler",
            IsBarding = key == "barding",
        };

        // Parse save bonus from combatSave string like "+1 (6+)", "+3 (4+)"
        if (!string.IsNullOrEmpty(data.CombatSave))
        {
            var match = Regex.Match(data.CombatSave, @"\+(\d+)");
            if (match.Success)
                armour.SaveBonus = int.Parse(match.Groups[1].Value);
        }

        // Parse special rules
        if (data.SpecialRules != null)
        {
            foreach (var rule in data.SpecialRules)
            {
                if (rule.Contains("Parry", StringComparison.OrdinalIgnoreCase))
                    armour.HasParry = true;
            }
        }

        return armour;
    }

    #endregion

    #region Table lookups

    public List<WeaponDefinition> GetCommonCloseCombatWeapons()
        => _weapons.Values.Where(w => w.IsCloseCombat).ToList();

    public WeaponDefinition? FindWeaponByName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace("-", "");
        return _weapons.Values.FirstOrDefault(w =>
            w.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Replace(" ", "").Replace("-", "")
                .Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public ArmourDefinition? FindArmourByName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace("-", "");
        return _armour.Values.FirstOrDefault(a =>
            a.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            a.Name.Replace(" ", "").Replace("-", "")
                .Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public int GetToHitValue(int attackerWS, int defenderWS)
    {
        if (_toHitTable.Length == 0) return 4;
        int row = Math.Clamp(attackerWS, 1, _toHitTable.Length) - 1;
        int col = Math.Clamp(defenderWS, 1, _toHitTable[0].Length) - 1;
        return _toHitTable[row][col];
    }

    public int GetToWoundValue(int strength, int toughness)
    {
        if (_toWoundTable.Length == 0) return 4;
        int row = Math.Clamp(strength, 1, _toWoundTable.Length) - 1;
        int col = Math.Clamp(toughness, 1, _toWoundTable[0].Length) - 1;
        return _toWoundTable[row][col];
    }

    /// <summary>
    /// Returns a positive penalty value for armour saves based on attacker strength.
    /// E.g., S3 → 0, S4 → 1, S5 → 2, S6 → 3.
    /// </summary>
    public int GetArmourSavePenalty(int strength)
    {
        if (_armourSavePenalties.TryGetValue(strength, out int penalty))
            return penalty;
        // Fallback: derived from PDF pattern (S<=3: 0, S4+: S-3)
        if (strength <= 3) return 0;
        return strength - 3;
    }

    #endregion
}
