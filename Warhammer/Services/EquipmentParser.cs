namespace Warhammer.Services;

using Warhammer.Models;

public class ParsedEquipment
{
    public WeaponDefinition? DefaultCloseCombatWeapon { get; set; }
    public List<ArmourDefinition> ArmourPieces { get; set; } = new();
    public List<string> UnrecognizedItems { get; set; } = new();

    public int CalculateBaseArmourSave()
    {
        int total = ArmourPieces.Sum(a => a.SaveBonus);
        return total > 0 ? 7 - total : 7; // 7 = no save
    }
}

public static class EquipmentParser
{
    public static ParsedEquipment Parse(string? equipmentText, CoreRulesService coreRules)
    {
        var result = new ParsedEquipment();
        if (string.IsNullOrWhiteSpace(equipmentText)) return result;

        var parts = equipmentText.Split(',').Select(p => p.Trim());
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            var weapon = coreRules.FindWeaponByName(part);
            if (weapon != null && weapon.IsCloseCombat)
            {
                result.DefaultCloseCombatWeapon ??= weapon;
                continue;
            }

            var armour = coreRules.FindArmourByName(part);
            if (armour != null)
            {
                result.ArmourPieces.Add(armour);
                continue;
            }

            result.UnrecognizedItems.Add(part);
        }

        result.DefaultCloseCombatWeapon ??= coreRules.HandWeapon;
        return result;
    }
}
