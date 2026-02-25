namespace Warhammer.Services;

public class WeaponDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsCloseCombat { get; set; }
    public int StrengthBonus { get; set; }
    public int AttackBonus { get; set; }
    public int InitiativeModifier { get; set; }
    public int ArmourPiercing { get; set; }
    public bool AlwaysStrikesLast { get; set; }
    public bool RequiresTwoHands { get; set; }
    public int FightInExtraRanks { get; set; }
    public bool HasParry { get; set; }

    public string EffectSummary
    {
        get
        {
            var parts = new List<string>();
            if (StrengthBonus != 0) parts.Add($"+{StrengthBonus}S");
            if (AttackBonus != 0) parts.Add($"+{AttackBonus}A");
            if (ArmourPiercing != 0) parts.Add($"AP{ArmourPiercing}");
            if (AlwaysStrikesLast) parts.Add("ASL");
            if (FightInExtraRanks > 0) parts.Add($"+{FightInExtraRanks} fighting rank(s)");
            if (HasParry) parts.Add("Parry");
            if (RequiresTwoHands) parts.Add("Two-handed");
            if (InitiativeModifier != 0) parts.Add($"+{InitiativeModifier}I");
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }
    }
}

public class ArmourDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int SaveBonus { get; set; }
    public bool IsShield { get; set; }
    public bool IsBarding { get; set; }
}

public class CoreRulesService
{
    private static readonly Dictionary<string, WeaponDefinition> _weapons = new()
    {
        ["handWeapon"] = new()
        {
            Key = "handWeapon", Name = "Hand Weapon", IsCloseCombat = true
        },
        ["additionalHandWeapon"] = new()
        {
            Key = "additionalHandWeapon", Name = "Additional Hand Weapon",
            IsCloseCombat = true, AttackBonus = 1, HasParry = true, RequiresTwoHands = true
        },
        ["greatWeapon"] = new()
        {
            Key = "greatWeapon", Name = "Great Weapon",
            IsCloseCombat = true, StrengthBonus = 2, ArmourPiercing = 1,
            AlwaysStrikesLast = true, RequiresTwoHands = true
        },
        ["halberd"] = new()
        {
            Key = "halberd", Name = "Halberd",
            IsCloseCombat = true, StrengthBonus = 1, ArmourPiercing = 1,
            RequiresTwoHands = true
        },
        ["polearm"] = new()
        {
            Key = "polearm", Name = "Polearm",
            IsCloseCombat = true, StrengthBonus = 1, RequiresTwoHands = true
        },
        ["spear"] = new()
        {
            Key = "spear", Name = "Spear",
            IsCloseCombat = true, FightInExtraRanks = 1, InitiativeModifier = 1
        },
        ["pike"] = new()
        {
            Key = "pike", Name = "Pike",
            IsCloseCombat = true, FightInExtraRanks = 3, InitiativeModifier = 2,
            RequiresTwoHands = true
        },
        ["flail"] = new()
        {
            Key = "flail", Name = "Flail",
            IsCloseCombat = true, StrengthBonus = 2, RequiresTwoHands = true
        },
        ["lightLance"] = new()
        {
            Key = "lightLance", Name = "Light Lance",
            IsCloseCombat = true, InitiativeModifier = 1
        },
        ["heavyLance"] = new()
        {
            Key = "heavyLance", Name = "Heavy Lance",
            IsCloseCombat = true, InitiativeModifier = 2
        },
        ["morningStar"] = new()
        {
            Key = "morningStar", Name = "Morning Star",
            IsCloseCombat = true, StrengthBonus = 1
        },
    };

    private static readonly Dictionary<string, ArmourDefinition> _armour = new()
    {
        ["lightArmour"] = new() { Key = "lightArmour", Name = "Light Armour", SaveBonus = 1 },
        ["mediumArmour"] = new() { Key = "mediumArmour", Name = "Medium Armour", SaveBonus = 2 },
        ["heavyArmour"] = new() { Key = "heavyArmour", Name = "Heavy Armour", SaveBonus = 3 },
        ["fullPlateArmour"] = new() { Key = "fullPlateArmour", Name = "Full Plate Armour", SaveBonus = 4 },
        ["chaosArmour"] = new() { Key = "chaosArmour", Name = "Chaos Armour", SaveBonus = 4 },
        ["shield"] = new() { Key = "shield", Name = "Shield", SaveBonus = 1, IsShield = true },
        ["barding"] = new() { Key = "barding", Name = "Barding", SaveBonus = 1, IsBarding = true },
    };

    public IReadOnlyDictionary<string, WeaponDefinition> Weapons => _weapons;
    public IReadOnlyDictionary<string, ArmourDefinition> Armour => _armour;

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

    // 10x10 WS-vs-WS to-hit table (rows = attacker WS 1-10, cols = target WS 1-10)
    private static readonly int[][] ToHitTable =
    [
        [4, 4, 5, 5, 6, 6, 6, 6, 6, 6],
        [3, 4, 4, 4, 5, 5, 6, 6, 6, 6],
        [2, 3, 4, 4, 4, 4, 5, 5, 6, 6],
        [2, 3, 3, 4, 4, 4, 4, 4, 5, 5],
        [2, 2, 3, 3, 4, 4, 4, 4, 4, 4],
        [2, 2, 3, 3, 3, 4, 4, 4, 4, 4],
        [2, 2, 2, 3, 3, 3, 4, 4, 4, 4],
        [2, 2, 2, 3, 3, 3, 3, 4, 4, 4],
        [2, 2, 2, 2, 3, 3, 3, 3, 4, 4],
        [2, 2, 2, 2, 3, 3, 3, 3, 3, 4],
    ];

    // 10x10 S-vs-T to-wound table (rows = strength 1-10, cols = toughness 1-10)
    private static readonly int[][] ToWoundTable =
    [
        [4, 5, 6, 6, 6, 6, 6, 6, 6, 6],
        [3, 4, 5, 6, 6, 6, 6, 6, 6, 6],
        [2, 3, 4, 5, 6, 6, 6, 6, 6, 6],
        [2, 2, 3, 4, 5, 6, 6, 6, 6, 6],
        [2, 2, 2, 3, 4, 5, 6, 6, 6, 6],
        [2, 2, 2, 2, 3, 4, 5, 6, 6, 6],
        [2, 2, 2, 2, 2, 3, 4, 5, 6, 6],
        [2, 2, 2, 2, 2, 2, 3, 4, 5, 6],
        [2, 2, 2, 2, 2, 2, 2, 3, 4, 5],
        [2, 2, 2, 2, 2, 2, 2, 2, 3, 4],
    ];

    public static int GetToHitValue(int attackerWS, int defenderWS)
    {
        int row = Math.Clamp(attackerWS, 1, 10) - 1;
        int col = Math.Clamp(defenderWS, 1, 10) - 1;
        return ToHitTable[row][col];
    }

    public static int GetToWoundValue(int strength, int toughness)
    {
        int row = Math.Clamp(strength, 1, 10) - 1;
        int col = Math.Clamp(toughness, 1, 10) - 1;
        return ToWoundTable[row][col];
    }

    public static int GetArmourSaveModifier(int strength)
    {
        if (strength <= 3) return 0;
        return -(strength - 3);
    }
}
