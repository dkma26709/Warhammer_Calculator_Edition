namespace Warhammer.Models;

public class FactionIndex
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
}

public class FactionData
{
    public string FactionName { get; set; } = "";
    public List<UnitData> Units { get; set; } = new();
}

public class UnitData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string UnitType { get; set; } = "Infantry";
    public StatLine Stats { get; set; } = new();
    public int? DefaultArmorSave { get; set; }
    public string DefaultEquipment { get; set; } = "";
    public int? WardSave { get; set; }
    public int? RegenerationSave { get; set; }
    public List<string> SpecialRules { get; set; } = new();
    public List<WeaponUpgrade> WeaponUpgrades { get; set; } = new();
    public List<ArmorUpgrade> ArmorUpgrades { get; set; } = new();
    public CommandGroupData? CommandGroup { get; set; }
    public List<MagicBannerData> MagicBanners { get; set; } = new();
    public MountData? Mount { get; set; }
}

public class StatLine
{
    public int Movement { get; set; }
    public int WeaponSkill { get; set; }
    public int BallisticSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Wounds { get; set; }
    public int Initiative { get; set; }
    public int Attacks { get; set; }
    public int Leadership { get; set; }
}

public class WeaponUpgrade
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int StrengthBonus { get; set; }
    public int AttackBonus { get; set; }
    public List<string> SpecialRules { get; set; } = new();
    public bool ReplacesDefault { get; set; }
}

public class ArmorUpgrade
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int SaveModifier { get; set; }
}

public class CommandGroupData
{
    public ChampionData? Champion { get; set; }
    public MusicianData? Musician { get; set; }
    public StandardBearerData? StandardBearer { get; set; }
}

public class ChampionData
{
    public bool Available { get; set; }
    public int AttackBonus { get; set; } = 1;
}

public class MusicianData
{
    public bool Available { get; set; }
}

public class StandardBearerData
{
    public bool Available { get; set; }
}

public class MagicBannerData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int CombatResolutionBonus { get; set; }
}

public class MountData
{
    public string Name { get; set; } = "";
    public int WeaponSkill { get; set; }
    public int Strength { get; set; }
    public int Toughness { get; set; }
    public int Attacks { get; set; }
    public int Initiative { get; set; }
}
