using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Rules the shipped data has to deliver whole. The old PDF extraction wrapped a unit's
/// comma-separated SPECIAL RULES list at the column edge and lost the tail; the bundle is
/// typeset from source, but these pin the recovered rules on shipped data all the same, since
/// that is where a regression would live.
/// </summary>
public class WrappedRuleListTests
{
    [Fact]
    public async Task HammerersAreStubborn()
    {
        var hammerers = await TestHelpers.FindUnitAsync("dwarfs", "HAMMERERS");

        Assert.True(hammerers.BaseModel.SpecialRules.Stubborn);
        Assert.Equal(1, hammerers.BaseModel.SpecialRules.ArmourPiercing);
    }

    [Fact]
    public async Task KhainiteAssassinsDodgeAndHate()
    {
        var assassin = await TestHelpers.FindUnitAsync("dark-elves", "KHAINITE ASSASSIN");

        Assert.Equal(5, assassin.BaseModel.SpecialRules.Dodge);
        Assert.True(assassin.BaseModel.SpecialRules.Hatred);
    }

    [Fact]
    public async Task TheCauldronOfBloodIsFrenziedAndHits()
    {
        var cauldron = await TestHelpers.FindUnitAsync("dark-elves", "CAULDRON OF BLOOD");

        Assert.True(cauldron.BaseModel.SpecialRules.Frenzy);
        Assert.Equal("D3", cauldron.BaseModel.SpecialRules.ImpactHitsRoll);
    }

    [Fact]
    public async Task TrollSlayersAreUnbreakable()
    {
        var slayers = await TestHelpers.FindUnitAsync("dwarfs", "TROLL SLAYERS");

        Assert.True(slayers.BaseModel.SpecialRules.Unbreakable);
    }

    /// <summary>Melusai Ironscale lost both a rule and a save to the same wrap.</summary>
    [Fact]
    public async Task MelusaiIronscaleKeepsNaturalArmourAndSwiftstride()
    {
        var ironscale = await TestHelpers.FindUnitAsync("dark-elves", "MELUSAI IRONSCALE");

        Assert.Equal(6, ironscale.BaseModel.SpecialRules.NaturalArmour);
        Assert.True(ironscale.BaseModel.SpecialRules.Swiftstride);
    }

    /// <summary>
    /// A rule list ending in a comma means its tail never made it into the source. These are
    /// defects in the book's own typesetting, listed rather than silently tolerated so a new
    /// one is loud.
    /// </summary>
    private static readonly string[] TailLostInSource =
    [
        "daemons-of-chaos/FLESH HOUNDS",
    ];

    /// <summary>
    /// No unit's rule list should end mid-list beyond the known cases above. This guards every
    /// faction at once rather than only the units named in this file.
    /// </summary>
    [Fact]
    public async Task NoUnitsRuleListEndsMidList()
    {
        var repo = TestHelpers.Repo;
        var stranded = new List<string>();

        foreach (var faction in repo.AllFactions)
        {
            foreach (var unit in await repo.GetUnitsAsync(faction.Id))
            {
                var raw = unit.RawEntry.SpecialRules;
                if (!string.IsNullOrWhiteSpace(raw) && raw.TrimEnd().EndsWith(','))
                    stranded.Add($"{faction.Id}/{unit.Name}");
            }
        }

        Assert.Equal(TailLostInSource.OrderBy(s => s), stranded.OrderBy(s => s));
    }
}
