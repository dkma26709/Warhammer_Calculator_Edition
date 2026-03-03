using System.Text.Json;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

public class FactionDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string DataDir = Path.Combine(
        FindProjectRoot(), "wwwroot", "data");

    private static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Warhammer.csproj")))
                return dir;
            // Also check sibling directory
            var sibling = Path.Combine(Path.GetDirectoryName(dir)!, "Warhammer");
            if (Directory.Exists(sibling) && File.Exists(Path.Combine(sibling, "Warhammer.csproj")))
                return sibling;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Warhammer project root");
    }

    [Fact]
    public void CoreRules_Deserializes()
    {
        var json = File.ReadAllText(Path.Combine(DataDir, "core-rules.json"));
        var data = JsonSerializer.Deserialize<CoreRulesData>(json, JsonOptions);
        Assert.NotNull(data);
        Assert.NotEmpty(data.CloseCombatToHit.Table);
        Assert.NotEmpty(data.Weapons.CloseCombat);
    }

    [Theory]
    [InlineData("beastmen")]
    [InlineData("bretonnia")]
    [InlineData("daemons-of-chaos")]
    [InlineData("dark-elves")]
    [InlineData("dwarfs")]
    [InlineData("high-elves")]
    [InlineData("lizardmen")]
    [InlineData("ogre-kingdoms")]
    [InlineData("orcs-and-goblins")]
    [InlineData("skaven")]
    [InlineData("the-empire")]
    [InlineData("tomb-kings")]
    [InlineData("vampire-counts")]
    [InlineData("warriors-of-chaos")]
    [InlineData("wood-elves")]
    public void FactionUnits_Deserialize(string factionKey)
    {
        var path = Path.Combine(DataDir, "factions", $"{factionKey}.json");
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactionData>(json, JsonOptions);
        Assert.NotNull(data);
        Assert.NotEmpty(data.Units);

        // Verify every unit has at least one profile
        foreach (var unit in data.Units)
        {
            Assert.NotEmpty(unit.Profiles);
            Assert.False(string.IsNullOrWhiteSpace(unit.Name), "Unit name should not be empty");
        }
    }

    [Theory]
    [InlineData("beastmen")]
    [InlineData("bretonnia")]
    [InlineData("daemons-of-chaos")]
    [InlineData("dark-elves")]
    [InlineData("dwarfs")]
    [InlineData("high-elves")]
    [InlineData("lizardmen")]
    [InlineData("ogre-kingdoms")]
    [InlineData("orcs-and-goblins")]
    [InlineData("skaven")]
    [InlineData("the-empire")]
    [InlineData("tomb-kings")]
    [InlineData("vampire-counts")]
    [InlineData("warriors-of-chaos")]
    [InlineData("wood-elves")]
    public void FactionExtras_Deserialize(string factionKey)
    {
        var path = Path.Combine(DataDir, "factions", $"{factionKey}-extras.json");
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactionExtrasData>(json, JsonOptions);
        Assert.NotNull(data);
    }
}
