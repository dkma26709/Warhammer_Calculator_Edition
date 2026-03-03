using System.Text.Json;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

public class CoreRulesServiceTests
{
    private static readonly CoreRulesData CoreData;

    static CoreRulesServiceTests()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data");
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
    }

    private static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Warhammer.csproj")))
                return dir;
            var sibling = Path.Combine(Path.GetDirectoryName(dir)!, "Warhammer");
            if (Directory.Exists(sibling) && File.Exists(Path.Combine(sibling, "Warhammer.csproj")))
                return sibling;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Warhammer project root");
    }

    private CoreRulesService CreateService()
    {
        // Create a service with pre-loaded data using reflection
        var service = new CoreRulesService(null!);
        var dataField = typeof(CoreRulesService).GetField("_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var loadedField = typeof(CoreRulesService).GetField("_loaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        dataField.SetValue(service, CoreData);
        loadedField.SetValue(service, true);
        return service;
    }

    [Theory]
    [InlineData(4, 4, 4)] // Equal WS
    [InlineData(3, 4, 4)] // Lower WS
    [InlineData(4, 3, 3)] // Higher WS
    [InlineData(1, 10, 6)] // Minimum vs Maximum (per table)
    public void GetToHitScore_ReturnsExpected(int attackerWS, int defenderWS, int expected)
    {
        var service = CreateService();
        Assert.Equal(expected, service.GetToHitScore(attackerWS, defenderWS));
    }

    [Theory]
    [InlineData(4, 3, 3)] // S > T
    [InlineData(3, 3, 4)] // S == T
    [InlineData(3, 4, 5)] // S < T
    [InlineData(3, 5, 6)] // S << T
    [InlineData(6, 3, 2)] // S >> T
    public void GetToWoundScore_ReturnsExpected(int strength, int toughness, int expected)
    {
        var service = CreateService();
        Assert.Equal(expected, service.GetToWoundScore(strength, toughness));
    }

    [Theory]
    [InlineData(3, 0)]  // S3: no modifier
    [InlineData(4, -1)] // S4: -1
    [InlineData(5, -2)] // S5: -2
    [InlineData(6, -3)] // S6: -3
    public void GetArmourSaveModifier_ReturnsExpected(int strength, int expected)
    {
        var service = CreateService();
        Assert.Equal(expected, service.GetArmourSaveModifier(strength));
    }

    [Fact]
    public void ParseWeaponStrength_AsUser()
    {
        Assert.Equal(4, CoreRulesService.ParseWeaponStrength("As user", 4));
    }

    [Fact]
    public void ParseWeaponStrength_Plus()
    {
        Assert.Equal(5, CoreRulesService.ParseWeaponStrength("+1", 4));
    }

    [Fact]
    public void ParseWeaponStrength_Fixed()
    {
        Assert.Equal(6, CoreRulesService.ParseWeaponStrength("6", 4));
    }

    [Fact]
    public void ParseArmourSave_Standard()
    {
        var (mod, save) = CoreRulesService.ParseArmourSave("+3/4+");
        Assert.Equal(3, mod);
        Assert.Equal(4, save);
    }

    [Fact]
    public void ParseArmourSave_Light()
    {
        var (mod, save) = CoreRulesService.ParseArmourSave("+1/6+");
        Assert.Equal(1, mod);
        Assert.Equal(6, save);
    }

    [Fact]
    public void GetCloseCombatWeapon_HandWeapon_Exists()
    {
        var service = CreateService();
        var weapon = service.GetCloseCombatWeapon("handWeapon");
        Assert.NotNull(weapon);
        Assert.Equal("As user", weapon.Strength);
    }

    [Fact]
    public void GetCloseCombatWeapon_GreatWeapon_Exists()
    {
        var service = CreateService();
        var weapon = service.GetCloseCombatWeapon("greatWeapon");
        Assert.NotNull(weapon);
        Assert.Equal("+1", weapon.Strength);
        Assert.Contains("Mighty Blow", weapon.SpecialRules);
    }

    [Fact]
    public void GetArmour_HeavyArmour_Exists()
    {
        var service = CreateService();
        var armour = service.GetArmour("heavyArmour");
        Assert.NotNull(armour);
        Assert.Equal("+3/4+", armour.CombatSave);
    }

    [Fact]
    public void GetArmour_Shields_Exists()
    {
        var service = CreateService();
        var armour = service.GetArmour("shields");
        Assert.NotNull(armour);
        Assert.Equal("+1/6+", armour.CombatSave);
    }

    [Fact]
    public void GetArmour_Invalid_ReturnsNull()
    {
        var service = CreateService();
        Assert.Null(service.GetArmour("nonexistent"));
    }
}
