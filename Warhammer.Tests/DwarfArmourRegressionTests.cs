using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Xunit.Abstractions;

namespace Warhammer.Tests;

/// <summary>
/// The matchup that surfaced the armour-stacking defect: 25 Saurus Warriors against Dwarf
/// Warriors and against Longbeards, both in heavy armour with great weapons. Longbeards are
/// strictly the better unit, so they must not be the easier fight.
/// </summary>
public class DwarfArmourRegressionTests
{
    private readonly ITestOutputHelper _output;
    public DwarfArmourRegressionTests(ITestOutputHelper output) => _output = output;

    /// <summary>Take the first option choice naming an item, wherever the book offers it.</summary>
    private static void Take(UnitConfiguration config, string itemName)
    {
        foreach (var group in config.ActiveGroups().ToList())
        {
            var choice = group.Choices.FirstOrDefault(c =>
                c.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
            if (choice is null) continue;

            if (group.GetSelectMode() == SelectMode.One)
                config.Selections.SelectOne(group.Id, choice.Id);
            else
                config.Selections.Toggle(group.Id, choice.Id, true);
            return;
        }
    }

    private static UnitConfiguration Kitted(Unit unit)
    {
        var config = new UnitConfiguration(unit) { ModelCount = 25, Frontage = 5 };
        Take(config, "great weapon");
        Take(config, "heavy armour");
        config.SelectedWeapon = config.EffectiveWeapons.Contains(CloseCombatWeaponKey.GreatWeapon)
            ? CloseCombatWeaponKey.GreatWeapon
            : config.SelectedWeapon;
        return config;
    }

    [Fact]
    public async Task Longbeards_AreNotAnEasierFightThanWarriors()
    {
        var repo = TestHelpers.Repo;
        var engine = new CombatEngine(repo);

        var saurusUnit = await TestHelpers.FindUnitAsync("lizardmen", "SAURUS WARRIORS");

        var results = new Dictionary<string, double>();
        foreach (var name in new[] { "WARRIORS", "LONGBEARDS" })
        {
            var opponent = Kitted(await TestHelpers.FindUnitAsync("dwarfs", name));
            var saurus = new UnitConfiguration(saurusUnit) { ModelCount = 25, Frontage = 5 };

            var result = engine.Simulate(saurus, opponent, new CombatContext
            {
                Charge = ChargeState.Neither,
                CombatRound = 1,
                AttackerFactionKey = "lizardmen",
                DefenderFactionKey = "dwarfs",
            }, 10_000);

            results[name] = result.AttackerWinPct;
            _output.WriteLine(
                $"vs {name,-12} save {opponent.EffectiveArmourSave(3, 0, 0)}+  " +
                $"saurus win {result.AttackerWinPct,5:F1}%  dwarf win {result.DefenderWinPct,5:F1}%  " +
                $"draw {result.DrawPct,5:F1}%");
        }

        Assert.True(results["LONGBEARDS"] <= results["WARRIORS"],
            $"Saurus won {results["LONGBEARDS"]:F1}% against Longbeards but only " +
            $"{results["WARRIORS"]:F1}% against the plainer Warriors — the better dwarf unit " +
            "should not be the easier fight.");
    }
}
