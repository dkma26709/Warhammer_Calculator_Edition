namespace Warhammer.Models;

public class ArmourDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int SaveBonus { get; set; }
    public bool IsShield { get; set; }
    public bool IsBuckler { get; set; }
    public bool IsBarding { get; set; }
    public bool HasParry { get; set; }
}
