using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;
using Xunit.Abstractions;

namespace Warhammer.Tests;

/// <summary>
/// Gauntlet comparison: Saurus Warriors (base vs WS4/I2) against all infantry.
/// Run with: dotnet test --filter SaurusGauntletTests -v n
/// </summary>
public class SaurusGauntletTests
{
    private readonly ITestOutputHelper _output;

    public SaurusGauntletTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string FactionName(GameDataRepository repo, string factionId) => repo.GetFactionDisplayName(factionId);

    /// <summary>Every non-character infantry unit in the edition, with its faction id.</summary>
    private static async Task<List<(Unit Unit, string Faction)>> AllInfantry(GameDataRepository repo)
    {
        var opponents = new List<(Unit Unit, string Faction)>();
        foreach (var faction in repo.AllFactions)
        {
            var units = await repo.GetUnitsAsync(faction.Id);
            foreach (var unit in units)
            {
                if (unit.BaseModel.TroopType.IsCharacter) continue;
                if (unit.Category == UnitCategory.CharacterMounts) continue;
                if (unit.BaseModel.TroopType.BaseType != BaseTroopType.Infantry) continue;
                opponents.Add((unit, faction.Id));
            }
        }
        return opponents;
    }

    [Fact]
    public async Task SaurusWarriors_BaseVsBuffed_GauntletComparison()
    {
        var repo = TestHelpers.Repo;
        var engine = new CombatEngine(repo);

        var saurusUnit = await TestHelpers.FindUnitAsync("lizardmen", "SAURUS WARRIORS");

        _output.WriteLine($"Subject: {saurusUnit.Name}");
        _output.WriteLine($"  Base stats: WS{saurusUnit.BaseModel.Stats.WS} S{saurusUnit.BaseModel.Stats.S} " +
                          $"T{saurusUnit.BaseModel.Stats.T} I{saurusUnit.BaseModel.Stats.I} A{saurusUnit.BaseModel.Stats.A}");
        _output.WriteLine($"  Cost: {saurusUnit.BaseModelCost} pts/model");
        _output.WriteLine("");

        var opponents = await AllInfantry(repo);

        _output.WriteLine($"Opponents: {opponents.Count} infantry units across all factions");
        _output.WriteLine(new string('=', 120));

        var baseResults = RunGauntlet(engine, repo, saurusUnit, opponents, modifier: null,
            "Saurus Warriors (BASE)");

        var buffModifier = new UnitModifier
        {
            Label = "WS+1 / I+1 buff",
            Source = "Manual",
            WeaponSkillBonus = 1,
            InitiativeBonus = 1,
        };
        var buffedResults = RunGauntlet(engine, repo, saurusUnit, opponents, buffModifier,
            "Saurus Warriors (BUFFED: WS+1, I+1)");

        _output.WriteLine("");
        _output.WriteLine(new string('=', 120));
        _output.WriteLine("COMPARISON: Base vs Buffed");
        _output.WriteLine(new string('=', 120));

        double totalBaseWin = 0, totalBuffWin = 0;
        for (int i = 0; i < opponents.Count; i++)
        {
            var (unit, faction) = opponents[i];
            var b = baseResults[i];
            var bf = buffedResults[i];
            totalBaseWin += b.AttackerWinPct;
            totalBuffWin += bf.AttackerWinPct;

            _output.WriteLine(
                $"{unit.Name,-35} {FactionName(repo, faction),-20} " +
                $"{b.AttackerWinPct,8:F1}% {bf.AttackerWinPct,8:F1}% {bf.AttackerWinPct - b.AttackerWinPct,+6:+0.0;-0.0}");
        }

        int n = opponents.Count;
        _output.WriteLine($"Overall win rate shift: {(totalBuffWin - totalBaseWin) / n:+0.0;-0.0} percentage points");

        // A buff to WS and I never makes the unit worse.
        Assert.True(totalBuffWin >= totalBaseWin);
    }

    private List<CombatSimulationResult> RunGauntlet(
        CombatEngine engine,
        GameDataRepository repo,
        Unit saurusUnit,
        List<(Unit Unit, string Faction)> opponents,
        UnitModifier? modifier,
        string label)
    {
        _output.WriteLine("");
        _output.WriteLine($"── {label} ──");

        var results = new List<CombatSimulationResult>();
        foreach (var (oppUnit, oppFaction) in opponents)
        {
            var saurusConfig = new UnitConfiguration(saurusUnit) { ModelCount = 25, Frontage = 5 };
            if (modifier != null)
                saurusConfig.Modifiers.Add(modifier);

            var oppConfig = new UnitConfiguration(oppUnit) { ModelCount = 25, Frontage = 5 };

            var context = new CombatContext
            {
                Charge = ChargeState.Neither,
                CombatRound = 1,
                AttackerFactionKey = "lizardmen",
                DefenderFactionKey = oppFaction,
            };

            var result = engine.Simulate(saurusConfig, oppConfig, context, 4_000);
            results.Add(result);

            _output.WriteLine(
                $"{oppUnit.Name,-35} {FactionName(repo, oppFaction),-20} " +
                $"{result.AttackerWinPct,7:F1}% {result.DefenderWinPct,7:F1}% {result.DrawPct,7:F1}% " +
                $"{result.AvgAttackerWounds,8:F2} {result.AvgDefenderWounds,8:F2}");
        }

        return results;
    }

    /// <summary>
    /// Dedicated CC units from a spread of factions vs Saurus Warriors, points-matched. A
    /// print-out rather than an assertion: the figures are what a designer reads.
    /// </summary>
    [Fact]
    public async Task SaurusVsDedicatedCCUnits_PointsMatched()
    {
        var repo = TestHelpers.Repo;
        var engine = new CombatEngine(repo);

        var saurusUnit = await TestHelpers.FindUnitAsync("lizardmen", "SAURUS WARRIORS");

        var ccUnits = new List<(string Label, Unit Unit, string Faction)>();

        async Task AddCC(string factionId, string nameContains, string label)
        {
            var units = await repo.GetUnitsAsync(factionId);
            var unit = units.FirstOrDefault(u =>
                u.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
                && !u.BaseModel.TroopType.IsCharacter
                && u.Category != UnitCategory.CharacterMounts);
            if (unit != null) ccUnits.Add((label, unit, factionId));
        }

        await AddCC("beastmen", "Bestigor", "Bestigor");
        await AddCC("daemons-of-chaos", "Bloodletters", "Bloodletters");
        await AddCC("dark-elves", "Executioners", "Executioners");
        await AddCC("dark-elves", "Witch Elves", "Witch Elves");
        await AddCC("dwarfs", "Hammerers", "Hammerers");
        await AddCC("dwarfs", "Ironbreakers", "Ironbreakers");
        await AddCC("high-elves", "Swordmasters", "Swordmasters");
        await AddCC("high-elves", "White Lions", "White Lions");
        await AddCC("high-elves", "Phoenix Guard", "Phoenix Guard");
        await AddCC("lizardmen", "Temple Guard", "Temple Guard");
        await AddCC("orcs-goblins", "Black Orc", "Black Orcs");
        await AddCC("skaven", "Stormvermin", "Stormvermin");
        await AddCC("skaven", "Plague Monk", "Plague Monks");
        await AddCC("empire", "Greatswords", "Greatswords");
        await AddCC("tomb-kings", "Tomb Guard", "Tomb Guard");
        await AddCC("vampire-counts", "Grave Guard", "Grave Guard");
        await AddCC("warriors-of-chaos", "Chaos Warriors", "Chaos Warriors");
        await AddCC("warriors-of-chaos", "Chosen", "Chosen");
        await AddCC("wood-elves", "Wildwood Rangers", "Wildwood Rangers");
        await AddCC("wood-elves", "Wardancers", "Wardancers");

        Assert.NotEmpty(ccUnits);

        _output.WriteLine("DEDICATED CC UNITS ROSTER:");
        foreach (var (label, unit, fk) in ccUnits)
        {
            var s = unit.BaseModel.Stats;
            _output.WriteLine($"{label,-24} {FactionName(repo, fk),-20} WS{s.WS} S{s.S} T{s.T} I{s.I} A{s.A} Ld{s.Ld} {unit.BaseModelCost,4:F0}pts");
        }

        double budget = 375.0;
        int saurusModels = Math.Max(1, (int)(budget / saurusUnit.BaseModelCost));
        double totalWin = 0;

        foreach (var (ccLabel, ccUnit, ccFaction) in ccUnits)
        {
            int oppModels = Math.Max(1, (int)(budget / Math.Max(1, ccUnit.BaseModelCost)));
            var saurusConfig = new UnitConfiguration(saurusUnit) { ModelCount = saurusModels, Frontage = 5 };
            var oppConfig = new UnitConfiguration(ccUnit) { ModelCount = oppModels, Frontage = 5 };
            var ctx = new CombatContext
            {
                Charge = ChargeState.Neither,
                CombatRound = 1,
                AttackerFactionKey = "lizardmen",
                DefenderFactionKey = ccFaction,
            };

            var result = engine.Simulate(saurusConfig, oppConfig, ctx, 4_000);
            totalWin += result.AttackerWinPct;

            _output.WriteLine(
                $"  vs {ccLabel,-20} {oppModels,4} {result.AttackerWinPct,6:F1}% {result.DefenderWinPct,6:F1}% {result.DrawPct,6:F1}%");
        }

        _output.WriteLine($"  AVERAGE win {totalWin / ccUnits.Count:F1}%");
    }

    /// <summary>
    /// Isolate why a Chaos Warrior statline (WS5/I4, 4+ save, no rules) performs similarly to a
    /// buffed Saurus (WS4/I2, 4+ save, Predatory Fighter). Uses synthetic units to control each
    /// variable independently.
    /// </summary>
    [Fact]
    public void ChaosWarriorVsSaurus_RuleAndStatIsolation()
    {
        var repo = TestHelpers.Repo;
        var engine = new CombatEngine(repo);

        var opponents = new List<(string Name, Unit Unit)>
        {
            ("Empire Spearmen",    TestHelpers.MakeUnit(name: "Spearmen",    ws: 3, s: 3, t: 3, i: 3, a: 1, ld: 7, points: 5,  equipment: "Hand weapon, shield", specialRules: "Fight in Extra Ranks (1)")),
            ("Orc Boyz",           TestHelpers.MakeUnit(name: "Orc Boyz",    ws: 3, s: 3, t: 4, i: 2, a: 1, ld: 7, points: 5,  equipment: "Hand weapon")),
            ("Dwarf Warriors",     TestHelpers.MakeUnit(name: "Dwarf Warriors", ws: 4, s: 3, t: 4, i: 2, a: 1, ld: 9, points: 8,  equipment: "Hand weapon, heavy armour")),
            ("Gor Herd",           TestHelpers.MakeUnit(name: "Gor Herd",    ws: 4, s: 3, t: 4, i: 3, a: 1, ld: 7, points: 7,  equipment: "Hand weapon")),
            ("Grave Guard",        TestHelpers.MakeUnit(name: "Grave Guard", ws: 3, s: 4, t: 4, i: 3, a: 1, ld: 6, points: 10, equipment: "Hand weapon, medium armour", specialRules: "Killing Blow")),
            ("Bestigor",           TestHelpers.MakeUnit(name: "Bestigor",    ws: 4, s: 4, t: 4, i: 3, a: 1, ld: 7, points: 10, equipment: "Hand weapon, light armour")),
            ("Black Orcs",         TestHelpers.MakeUnit(name: "Black Orcs",  ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14, equipment: "Hand weapon, heavy armour", specialRules: "Mighty Blow (1)")),
            ("Swordmasters",       TestHelpers.MakeUnit(name: "Swordmasters",ws: 6, s: 3, t: 3, i: 6, a: 2, ld: 8, points: 16, equipment: "Great weapon, heavy armour", specialRules: "Martial Prowess")),
        };

        var subjects = new List<(string Label, Unit Unit)>
        {
            ("CW statline (WS5/I4, 5+ sv)",
                TestHelpers.MakeUnit(name: "CW-like", ws: 5, s: 4, t: 4, i: 4, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, heavy armour")),
            ("Buff Saurus (WS4/I2, 4+ sv, PF+CB)",
                TestHelpers.MakeUnit(name: "Saurus-like", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Cold-blooded, Predatory Fighter, Natural Armour (5+)")),
            ("Buff Saurus NO PredFighter",
                TestHelpers.MakeUnit(name: "Saurus-noPF", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Cold-blooded, Natural Armour (5+)")),
            ("CW + shield (WS5/I4, 4+ sv)",
                TestHelpers.MakeUnit(name: "CW-shield", ws: 5, s: 4, t: 4, i: 4, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, heavy armour, shield")),
        };

        _output.WriteLine($"{"Subject",-38} {"Avg Win%",9} {"Avg Loss%",10}");
        foreach (var (label, subjectUnit) in subjects)
        {
            double totalWin = 0, totalLoss = 0;

            foreach (var (_, oppUnit) in opponents)
            {
                var subjectConfig = new UnitConfiguration(subjectUnit) { ModelCount = 25, Frontage = 5 };
                var oppConfig = new UnitConfiguration(oppUnit) { ModelCount = 25, Frontage = 5 };
                var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };

                var result = engine.Simulate(subjectConfig, oppConfig, context, 4_000);
                totalWin += result.AttackerWinPct;
                totalLoss += result.DefenderWinPct;
            }

            _output.WriteLine($"{label,-38} {totalWin / opponents.Count,8:F1}% {totalLoss / opponents.Count,9:F1}%");
        }
    }
}
