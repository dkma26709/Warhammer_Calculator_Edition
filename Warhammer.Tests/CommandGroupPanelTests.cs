using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Warhammer.Components;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;

namespace Warhammer.Tests;

/// <summary>
/// The command group panel rendered to HTML, so the controls a command model's sub-options get
/// are checked without a browser: a dropdown for a choice, a checkbox for a single item.
/// </summary>
public class CommandGroupPanelTests
{
    /// <summary>Empire Handgunners as WAR writes them: the Leader may swap his handgun.</summary>
    private static Unit Handgunners() => TestHelpers.MakeUnit(
        name: "Handgunners",
        equipment: "Hand weapon, handgun",
        options:
        [
            new UnitOption
            {
                Type = "command", Role = "Leader", Points = 5,
                SubOptions =
                [
                    new UnitOption
                    {
                        Type = "equipment", Action = "replace", Replaces = "handgun",
                        Raw = "May replace handgun with one of the following:",
                        Choices =
                        [
                            new OptionChoice { Name = ["Brace of pistols"], Points = 0, Replaces = "handgun" },
                            new OptionChoice { Name = ["Repeater handgun"], Points = 5, Replaces = "handgun" },
                            new OptionChoice { Name = ["Hochland long rifle"], Points = 10, Replaces = "handgun" },
                        ],
                    },
                ],
            },
            new UnitOption { Type = "command", Role = "Musician", Points = 5 },
            new UnitOption
            {
                Type = "command", Role = "Standard Bearer", Points = 10,
                SubOptions =
                [
                    new UnitOption { Type = "magic_standard", PointsLimit = 25 },
                    new UnitOption
                    {
                        Type = "upgrade", Upgrade = "Look-out Gnoblar 5 points",
                        Raw = "May take a Look-out Gnoblar 5 points",
                    },
                ],
            },
        ]);

    private static async Task<string> Render(UnitConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CommandGroupPanel.Configuration)] = configuration,
            });
            var output = await renderer.RenderComponentAsync<CommandGroupPanel>(parameters);
            // Decoded, so a test reads "(+5 pts)" the way the page shows it.
            return System.Net.WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    [Fact]
    public async Task ALeaderWithAChoiceOfWeaponsGetsADropdown()
    {
        var html = await Render(new UnitConfiguration(Handgunners()) { HasChampion = true });

        Assert.Contains("Leader: replace handgun with", html);
        Assert.Contains("<select", html);
        Assert.Contains("Brace of pistols (free)", html);
        Assert.Contains("Repeater handgun (+5 pts)", html);
        Assert.Contains("Hochland long rifle (+10 pts)", html);
    }

    [Fact]
    public async Task TheDropdownShowsThePickMade()
    {
        var cfg = new UnitConfiguration(Handgunners()) { HasChampion = true };
        cfg.SelectCommandUpgrade(0, 0, 2);

        var html = await Render(cfg);

        Assert.Contains("<select", html);
        Assert.Contains("value=\"2\"", html);
    }

    [Fact]
    public async Task ARoleNotTakenOffersNothing()
    {
        var html = await Render(new UnitConfiguration(Handgunners()));

        Assert.Contains("Leader", html);
        Assert.DoesNotContain("Hochland", html);
        Assert.DoesNotContain("Look-out", html);
        Assert.DoesNotContain("<select", html);
    }

    [Fact]
    public async Task AStandardBearersUpgradeIsACheckboxAndTheBannerIsNot()
    {
        var html = await Render(new UnitConfiguration(Handgunners()) { HasStandardBearer = true });

        Assert.Contains("Standard Bearer: Look-out Gnoblar (+5 pts)", html);
        Assert.DoesNotContain("magic_standard", html);
        Assert.DoesNotContain("<select", html);
    }
}
