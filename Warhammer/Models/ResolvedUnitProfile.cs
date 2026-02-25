using Warhammer.Calculator;

namespace Warhammer.Models;

public class ResolvedUnitProfile : IUnit
{
    public string Name { get; set; } = "";
    public string FactionName { get; set; } = "";
    public string EquipmentDescription { get; set; } = "";

    public UnitType UnitType { get; set; } = UnitType.Infantry;
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
    public int? ArmorSave { get; set; }
    public int? WardSave { get; set; }
    public int? RegenerationSave { get; set; }
    public List<SpecialRule> SpecialRules { get; set; } = new();
    public List<Weapon> Weapons { get; set; } = new();
    public List<RangedWeapon> RangedWeapons { get; set; } = new();
    public List<Armor> Armors { get; set; } = new();

    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }
    public MagicBannerData? SelectedBanner { get; set; }
    public MountData? Mount { get; set; }

    public static ResolvedUnitProfile FromUnitData(
        UnitData unitData,
        string factionName,
        WeaponUpgrade? selectedWeapon,
        ArmorUpgrade? selectedArmor,
        bool hasChampion,
        bool hasMusician,
        bool hasStandard,
        MagicBannerData? selectedBanner)
    {
        var profile = new ResolvedUnitProfile
        {
            Name = unitData.Name,
            FactionName = factionName,
            UnitType = unitData.UnitType == "Cavalry" ? UnitType.Cavalry : UnitType.Infantry,
            Movement = unitData.Stats.Movement,
            WeaponSkill = unitData.Stats.WeaponSkill,
            BallisticSkill = unitData.Stats.BallisticSkill,
            Strength = unitData.Stats.Strength,
            Toughness = unitData.Stats.Toughness,
            Wounds = unitData.Stats.Wounds,
            Initiative = unitData.Stats.Initiative,
            Attacks = unitData.Stats.Attacks,
            Leadership = unitData.Stats.Leadership,
            ArmorSave = unitData.DefaultArmorSave,
            WardSave = unitData.WardSave,
            RegenerationSave = unitData.RegenerationSave,
            EquipmentDescription = unitData.DefaultEquipment,
            Mount = unitData.Mount,
            HasChampion = hasChampion,
            HasMusician = hasMusician,
            HasStandardBearer = hasStandard,
            SelectedBanner = selectedBanner,
        };

        if (selectedWeapon is not null)
        {
            profile.Strength += selectedWeapon.StrengthBonus;
            profile.Attacks += selectedWeapon.AttackBonus;
        }

        if (selectedArmor is not null && profile.ArmorSave.HasValue)
        {
            profile.ArmorSave = Math.Max(profile.ArmorSave.Value + selectedArmor.SaveModifier, 1);
        }

        return profile;
    }
}
