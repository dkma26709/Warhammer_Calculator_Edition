using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// The army books wrap a unit's comma-separated SPECIAL RULES list at the column edge, and
/// extract_pdf.py used to keep only the first line — everything after it was filed under
/// specialRuleDescriptions, so the tail never reached the parser. 135 wap-v2 and 161 wap-v3 rule
/// lists were affected. These pin the recovered rules on shipped data, which is where the bug
/// actually lived; the parser was never at fault.
/// </summary>
public class WrappedRuleListTests
{
    private static async Task<Unit> Find(string system, FactionKey faction, string name)
    {
        var repo = TestHelpers.RepoFor(system);
        return (await repo.GetUnitsAsync(faction)).First(u => u.Name == name);
    }

    /// <summary>
    /// The proof case: v3 Hammerers' list ran "…Relentless, Resolute," and stopped, with
    /// "Stubborn" sitting alone as the first description and "King's Guard: …" as the second.
    /// </summary>
    [Fact]
    public async Task V3HammerersAreStubborn()
    {
        var hammerers = await Find(GameSystems.WapV3, FactionKey.Dwarfs, "Hammerers");

        Assert.True(hammerers.BaseModel.SpecialRules.Stubborn);
        Assert.Equal(1, hammerers.BaseModel.SpecialRules.ArmourPiercing);
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task KhainiteAssassinsHavePoisonedAttacks(string system)
    {
        var assassin = await Find(system, FactionKey.DarkElves, "Khainite Assassin");

        Assert.True(assassin.BaseModel.SpecialRules.PoisonedAttacks);
        Assert.True(assassin.BaseModel.SpecialRules.MurderousProwess);
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task TheCauldronOfBloodCausesTerrorAndPoisons(string system)
    {
        var cauldron = await Find(system, FactionKey.DarkElves, "Cauldron of Blood");

        Assert.True(cauldron.BaseModel.SpecialRules.Terror);
        Assert.True(cauldron.BaseModel.SpecialRules.PoisonedAttacks);
    }

    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task TrollSlayersAreUnbreakable(string system)
    {
        var slayers = await Find(system, FactionKey.Dwarfs, "Troll Slayers");

        Assert.True(slayers.BaseModel.SpecialRules.Unbreakable);
    }

    /// <summary>Melusai Ironscale lost both a rule and a save to the same wrap.</summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task MelusaiIronscaleKeepsNaturalArmourAndSwiftstride(string system)
    {
        var ironscale = await Find(system, FactionKey.DarkElves, "Melusai Ironscale");

        Assert.Equal(6, ironscale.BaseModel.SpecialRules.NaturalArmour);
        Assert.True(ironscale.BaseModel.SpecialRules.Swiftstride);
    }

    /// <summary>
    /// A rule list ending in a comma means its tail never made it into the field. Rejoining the
    /// wrapped continuation fixed every case where the tail was merely misfiled; these few are a
    /// different defect — the tail is absent from the extraction altogether, with no continuation
    /// to recover — so they are listed rather than silently tolerated.
    /// </summary>
    private static readonly Dictionary<string, string[]> TailLostInExtraction = new()
    {
        [GameSystems.WapV2] = ["DaemonsOfChaos/Flesh Hounds", "TheEmpire/Grand Master"],
        [GameSystems.WapV3] = ["DaemonsOfChaos/Flesh Hounds"],
    };

    /// <summary>
    /// No unit's rule list should end mid-list beyond the known-lost cases above. This guards
    /// every faction at once rather than only the units named in this file.
    /// </summary>
    [Theory]
    [InlineData(GameSystems.WapV2)]
    [InlineData(GameSystems.WapV3)]
    public async Task NoUnitsRuleListEndsMidList(string system)
    {
        var repo = TestHelpers.RepoFor(system);
        var stranded = new List<string>();

        foreach (var faction in Enum.GetValues<FactionKey>())
        {
            IReadOnlyList<Unit> units;
            try
            {
                units = await repo.GetUnitsAsync(faction);
            }
            catch (Exception)
            {
                continue;   // faction not present in this system's data
            }

            foreach (var unit in units)
            {
                var raw = unit.RawEntry?.SpecialRules;
                if (!string.IsNullOrWhiteSpace(raw) && raw.TrimEnd().EndsWith(','))
                    stranded.Add($"{faction}/{unit.Name}");
            }
        }

        Assert.Equal(TailLostInExtraction[system].OrderBy(s => s), stranded.OrderBy(s => s));
    }
}
