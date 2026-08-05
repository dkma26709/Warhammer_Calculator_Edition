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

    private static int OptionIndexFor(Unit unit, string itemName) => unit.Options
        .Select((o, idx) => (o, idx))
        .Where(x => x.o.Choices?.Any(c =>
            c.Name.Any(n => n.Contains(itemName, StringComparison.OrdinalIgnoreCase))) == true)
        .Select(x => x.idx)
        .DefaultIfEmpty(-1)
        .First();

    private static UnitConfiguration Kitted(Unit unit)
    {
        var config = new UnitConfiguration(unit) { ModelCount = 25, Frontage = 5 };

        foreach (var item in new[] { "great weapon", "heavy armour" })
        {
            int optionIdx = OptionIndexFor(unit, item);
            if (optionIdx < 0) continue;

            var choices = unit.Options[optionIdx].Choices!;
            int choiceIdx = choices.FindIndex(c =>
                c.Name.Any(n => n.Contains(item, StringComparison.OrdinalIgnoreCase)));
            config.SelectedUpgrades[optionIdx] = choiceIdx;
        }

        return config;
    }

    [Fact]
    public async Task Longbeards_AreNotAnEasierFightThanWarriors()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        var saurusUnit = (await repo.GetUnitsAsync(FactionKey.Lizardmen))
            .First(u => u.Name.Contains("Saurus Warrior", StringComparison.OrdinalIgnoreCase));
        var dwarfs = await repo.GetUnitsAsync(FactionKey.Dwarfs);

        var results = new Dictionary<string, double>();
        foreach (var name in new[] { "Warriors", "Longbeards" })
        {
            var opponent = Kitted(dwarfs.First(u => u.Name == name));
            var saurus = new UnitConfiguration(saurusUnit) { ModelCount = 25, Frontage = 5 };

            var result = engine.Simulate(saurus, opponent, new CombatContext
            {
                Charge = ChargeState.Neither,
                CombatRound = 1,
                AttackerFactionKey = FactionKey.Lizardmen.ToJsonKey(),
                DefenderFactionKey = FactionKey.Dwarfs.ToJsonKey(),
            }, 10_000);

            results[name] = result.AttackerWinPct;
            _output.WriteLine(
                $"vs {name,-12} save {opponent.EffectiveArmourSave(3, 0, 0)}+  " +
                $"saurus win {result.AttackerWinPct,5:F1}%  dwarf win {result.DefenderWinPct,5:F1}%  " +
                $"draw {result.DrawPct,5:F1}%");
        }

        Assert.True(results["Longbeards"] <= results["Warriors"],
            $"Saurus won {results["Longbeards"]:F1}% against Longbeards but only " +
            $"{results["Warriors"]:F1}% against the plainer Warriors — the better dwarf unit " +
            "should not be the easier fight.");
    }
}
