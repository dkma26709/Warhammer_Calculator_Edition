using System.Net;
using System.Text;
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

    [Fact]
    public async Task SaurusWarriors_BaseVsBuffed_GauntletComparison()
    {
        // ── Set up a full repository that can load faction data from disk ──
        var repo = CreateFullRepository();
        var engine = new CombatEngine(repo);

        // ── Find Saurus Warriors ──
        var lizardmenUnits = await repo.GetUnitsAsync(FactionKey.Lizardmen);
        var saurusUnit = lizardmenUnits.FirstOrDefault(u =>
            u.Name.Contains("Saurus Warrior", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(saurusUnit);

        _output.WriteLine($"Subject: {saurusUnit.Name}");
        _output.WriteLine($"  Base stats: WS{saurusUnit.BaseModel.Stats.WS} S{saurusUnit.BaseModel.Stats.S} " +
                          $"T{saurusUnit.BaseModel.Stats.T} I{saurusUnit.BaseModel.Stats.I} A{saurusUnit.BaseModel.Stats.A}");
        _output.WriteLine($"  Cost: {saurusUnit.BaseModelCost} pts/model");
        _output.WriteLine($"  Rules: {saurusUnit.BaseModel.SpecialRules}");
        _output.WriteLine("");

        // ── Collect all non-character Infantry from every faction ──
        var opponents = new List<(Unit Unit, FactionKey Faction)>();
        foreach (var fk in FactionKeyExtensions.All)
        {
            var units = await repo.GetUnitsAsync(fk);
            foreach (var unit in units)
            {
                if (unit.BaseModel.TroopType.IsCharacter) continue;
                if (unit.BaseModel.TroopType.BaseType != BaseTroopType.Infantry) continue;
                opponents.Add((unit, fk));
            }
        }

        _output.WriteLine($"Opponents: {opponents.Count} infantry units across all factions");
        _output.WriteLine(new string('=', 120));

        // ── Run gauntlet: BASE Saurus Warriors ──
        var baseResults = RunGauntlet(engine, saurusUnit, opponents, modifier: null,
            "Saurus Warriors (BASE: WS3, I1)");

        // ── Run gauntlet: BUFFED Saurus Warriors (WS4, I2) ──
        // Base stats are WS3, I1 — need +1 WS, +1 I
        var buffModifier = new UnitModifier
        {
            Label = "WS4 / I2 buff",
            Source = "Manual",
            WeaponSkillBonus = 1,
            InitiativeBonus = 1,
        };
        var buffedResults = RunGauntlet(engine, saurusUnit, opponents, buffModifier,
            "Saurus Warriors (BUFFED: WS4, I2)");

        // ── Print comparison ──
        _output.WriteLine("");
        _output.WriteLine(new string('=', 120));
        _output.WriteLine("COMPARISON: Base vs Buffed (WS4, I2)");
        _output.WriteLine(new string('=', 120));
        _output.WriteLine(
            $"{"Opponent",-35} {"Faction",-20} " +
            $"{"Base Win%",9} {"Buff Win%",9} {"Delta",7} | " +
            $"{"Base Dealt",10} {"Buff Dealt",10} {"Delta",7} | " +
            $"{"Base Taken",10} {"Buff Taken",10} {"Delta",7}");
        _output.WriteLine(new string('-', 120));

        double totalBaseWin = 0, totalBuffWin = 0;
        double totalBaseDealt = 0, totalBuffDealt = 0;
        double totalBaseTaken = 0, totalBuffTaken = 0;

        for (int i = 0; i < opponents.Count; i++)
        {
            var (unit, faction) = opponents[i];
            var b = baseResults[i];
            var bf = buffedResults[i];
            double winDelta = bf.AttackerWinPct - b.AttackerWinPct;
            double dealtDelta = bf.AvgAttackerWounds - b.AvgAttackerWounds;
            double takenDelta = bf.AvgDefenderWounds - b.AvgDefenderWounds;

            totalBaseWin += b.AttackerWinPct;
            totalBuffWin += bf.AttackerWinPct;
            totalBaseDealt += b.AvgAttackerWounds;
            totalBuffDealt += bf.AvgAttackerWounds;
            totalBaseTaken += b.AvgDefenderWounds;
            totalBuffTaken += bf.AvgDefenderWounds;

            _output.WriteLine(
                $"{unit.Name,-35} {faction.ToDisplayName(),-20} " +
                $"{b.AttackerWinPct,8:F1}% {bf.AttackerWinPct,8:F1}% {winDelta,+6:+0.0;-0.0} | " +
                $"{b.AvgAttackerWounds,10:F2} {bf.AvgAttackerWounds,10:F2} {dealtDelta,+6:+0.0;-0.0} | " +
                $"{b.AvgDefenderWounds,10:F2} {bf.AvgDefenderWounds,10:F2} {takenDelta,+6:+0.0;-0.0}");
        }

        int n = opponents.Count;
        _output.WriteLine(new string('-', 120));
        _output.WriteLine(
            $"{"AVERAGE",-35} {"("+ n + " matchups)",-20} " +
            $"{totalBaseWin / n,8:F1}% {totalBuffWin / n,8:F1}% {(totalBuffWin - totalBaseWin) / n,+6:+0.0;-0.0} | " +
            $"{totalBaseDealt / n,10:F2} {totalBuffDealt / n,10:F2} {(totalBuffDealt - totalBaseDealt) / n,+6:+0.0;-0.0} | " +
            $"{totalBaseTaken / n,10:F2} {totalBuffTaken / n,10:F2} {(totalBuffTaken - totalBaseTaken) / n,+6:+0.0;-0.0}");

        _output.WriteLine("");
        _output.WriteLine($"Overall win rate shift: {(totalBuffWin - totalBaseWin) / n:+0.0;-0.0} percentage points");
    }

    // ── Helpers ──

    private List<CombatSimulationResult> RunGauntlet(
        CombatEngine engine,
        Unit saurusUnit,
        List<(Unit Unit, FactionKey Faction)> opponents,
        UnitModifier? modifier,
        string label)
    {
        _output.WriteLine("");
        _output.WriteLine($"── {label} ──");
        _output.WriteLine(
            $"{"Opponent",-35} {"Faction",-20} {"Win%",8} {"Loss%",8} {"Draw%",8} {"Dealt",8} {"Taken",8}");
        _output.WriteLine(new string('-', 100));

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
                AttackerFactionKey = FactionKey.Lizardmen.ToJsonKey(),
                DefenderFactionKey = oppFaction.ToJsonKey(),
            };

            var result = engine.Simulate(saurusConfig, oppConfig, context, 10_000);
            results.Add(result);

            _output.WriteLine(
                $"{oppUnit.Name,-35} {oppFaction.ToDisplayName(),-20} " +
                $"{result.AttackerWinPct,7:F1}% {result.DefenderWinPct,7:F1}% {result.DrawPct,7:F1}% " +
                $"{result.AvgAttackerWounds,8:F2} {result.AvgDefenderWounds,8:F2}");
        }

        double avgWin = results.Average(r => r.AttackerWinPct);
        double avgLoss = results.Average(r => r.DefenderWinPct);
        _output.WriteLine(new string('-', 100));
        _output.WriteLine($"{"AVERAGE",-56} {avgWin,7:F1}% {avgLoss,7:F1}%");

        return results;
    }

    [Fact]
    public async Task SaurusWarriors_PointsCostAnalysis()
    {
        var repo = CreateFullRepository();
        var engine = new CombatEngine(repo);

        // ── Load real units for direct comparison ──
        var lizardmenUnits = await repo.GetUnitsAsync(FactionKey.Lizardmen);
        var saurusUnit = lizardmenUnits.First(u =>
            u.Name.Contains("Saurus Warrior", StringComparison.OrdinalIgnoreCase));

        var orcUnits = await repo.GetUnitsAsync(FactionKey.OrcsAndGoblins);
        var blackOrcUnit = orcUnits.First(u =>
            u.Name.Contains("Black Orc", StringComparison.OrdinalIgnoreCase));

        var chaosUnits = await repo.GetUnitsAsync(FactionKey.WarriorsOfChaos);
        var chaosWarriorUnit = chaosUnits.First(u =>
            u.Name.Equals("Chaos Warriors", StringComparison.OrdinalIgnoreCase));

        // ── Collect all non-character infantry opponents ──
        var opponents = new List<(Unit Unit, FactionKey Faction)>();
        foreach (var fk in FactionKeyExtensions.All)
        {
            var units = await repo.GetUnitsAsync(fk);
            foreach (var unit in units)
            {
                if (unit.BaseModel.TroopType.IsCharacter) continue;
                if (unit.BaseModel.TroopType.BaseType != BaseTroopType.Infantry) continue;
                // Exclude self-matchups for cleaner data
                if (unit.Name == saurusUnit.Name && fk == FactionKey.Lizardmen) continue;
                if (unit.Name == blackOrcUnit.Name && fk == FactionKey.OrcsAndGoblins) continue;
                if (unit.Name == chaosWarriorUnit.Name && fk == FactionKey.WarriorsOfChaos) continue;
                opponents.Add((unit, fk));
            }
        }

        // ── Points budget: 375pts (equivalent to 25 Saurus at 15pts each) ──
        // We'll test at different price points to see where the buffed Saurus
        // achieves comparable win rates to Black Orcs (14pts) and Chaos Warriors (14pts)
        double budget = 375.0;

        _output.WriteLine("POINTS-MATCHED GAUNTLET: Each side spends ~375 pts");
        _output.WriteLine("Models per side = floor(375 / cost_per_model)");
        _output.WriteLine(new string('=', 130));

        var configs = new List<(string Label, Unit Unit, UnitModifier? Mod, double CostPerModel)>
        {
            ("Saurus BASE (13pts)", saurusUnit, null, 13),
            ("Saurus BUFF @14pts", saurusUnit, new UnitModifier { Label = "WS4/I2", Source = "Manual", WeaponSkillBonus = 1, InitiativeBonus = 1 }, 14),
            ("Saurus BUFF @15pts", saurusUnit, new UnitModifier { Label = "WS4/I2", Source = "Manual", WeaponSkillBonus = 1, InitiativeBonus = 1 }, 15),
            ("Saurus BUFF @16pts", saurusUnit, new UnitModifier { Label = "WS4/I2", Source = "Manual", WeaponSkillBonus = 1, InitiativeBonus = 1 }, 16),
            ("Black Orcs (14pts)", blackOrcUnit, null, 14),
            ("Chaos Warriors (14pts)", chaosWarriorUnit, null, 14),
        };

        // Header
        _output.WriteLine(
            $"{"Config",-28} {"Models",6} {"Pts",5} | {"Avg Win%",9} {"Avg Loss%",9} {"Avg Draw%",9} | {"Avg Dealt",9} {"Avg Taken",9} | {"Best",7} {"Worst",7}");
        _output.WriteLine(new string('-', 130));

        foreach (var (label, unit, mod, costPerModel) in configs)
        {
            int models = (int)(budget / costPerModel);
            double totalWin = 0, totalLoss = 0, totalDraw = 0;
            double totalDealt = 0, totalTaken = 0;
            double bestWin = 0, worstWin = 100;

            foreach (var (oppUnit, oppFaction) in opponents)
            {
                var config = new UnitConfiguration(unit) { ModelCount = models, Frontage = 5 };
                if (mod != null) config.Modifiers.Add(mod);

                // Opponent also gets ~375 pts worth of models
                int oppModels = Math.Max(1, (int)(budget / oppUnit.BaseModelCost));
                var oppConfig = new UnitConfiguration(oppUnit) { ModelCount = oppModels, Frontage = 5 };

                var context = new CombatContext
                {
                    Charge = ChargeState.Neither,
                    CombatRound = 1,
                    AttackerFactionKey = unit.Faction.ToJsonKey(),
                    DefenderFactionKey = oppFaction.ToJsonKey(),
                };

                var result = engine.Simulate(config, oppConfig, context, 5_000);
                totalWin += result.AttackerWinPct;
                totalLoss += result.DefenderWinPct;
                totalDraw += result.DrawPct;
                totalDealt += result.AvgAttackerWounds;
                totalTaken += result.AvgDefenderWounds;
                if (result.AttackerWinPct > bestWin) bestWin = result.AttackerWinPct;
                if (result.AttackerWinPct < worstWin) worstWin = result.AttackerWinPct;
            }

            int n = opponents.Count;
            _output.WriteLine(
                $"{label,-28} {models,6} {models * costPerModel,5:F0} | " +
                $"{totalWin / n,8:F1}% {totalLoss / n,8:F1}% {totalDraw / n,8:F1}% | " +
                $"{totalDealt / n,9:F2} {totalTaken / n,9:F2} | " +
                $"{bestWin,6:F1}% {worstWin,6:F1}%");
        }

        _output.WriteLine(new string('=', 130));
        _output.WriteLine("");
        _output.WriteLine("If Buffed Saurus at price X has comparable avg win% to Black Orcs/Chaos Warriors,");
        _output.WriteLine("that price point is 'fair' relative to the existing meta.");
    }

    /// <summary>
    /// Dedicated CC units from each faction vs Saurus Warriors (base and buffed),
    /// at multiple price points. Tests both equal-models and points-matched.
    /// </summary>
    [Fact]
    public async Task SaurusVsDedicatedCCUnits_FullAnalysis()
    {
        var repo = CreateFullRepository();
        var engine = new CombatEngine(repo);

        // ── Load Saurus Warriors ──
        var lizardmenUnits = await repo.GetUnitsAsync(FactionKey.Lizardmen);
        var saurusUnit = lizardmenUnits.First(u =>
            u.Name.Contains("Saurus Warrior", StringComparison.OrdinalIgnoreCase));

        // ── Load one dedicated CC unit from each faction ──
        var ccUnits = new List<(string Label, Unit Unit, FactionKey Faction)>();

        async Task AddCC(FactionKey fk, string nameContains, string label)
        {
            var units = await repo.GetUnitsAsync(fk);
            var unit = units.FirstOrDefault(u =>
                u.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase)
                && !u.BaseModel.TroopType.IsCharacter);
            if (unit != null) ccUnits.Add((label, unit, fk));
        }

        await AddCC(FactionKey.Beastmen, "Bestigor", "Bestigor");
        await AddCC(FactionKey.Bretonnia, "Foot Knights", "Foot Knights");
        await AddCC(FactionKey.DaemonsOfChaos, "Bloodletters", "Bloodletters");
        await AddCC(FactionKey.DarkElves, "Har Ganeth Executioners", "Executioners");
        await AddCC(FactionKey.DarkElves, "Witch Elves", "Witch Elves");
        await AddCC(FactionKey.Dwarfs, "Hammerers", "Hammerers");
        await AddCC(FactionKey.Dwarfs, "Ironbreakers", "Ironbreakers");
        await AddCC(FactionKey.HighElves, "Swordmasters", "Swordmasters");
        await AddCC(FactionKey.HighElves, "White Lions", "White Lions");
        await AddCC(FactionKey.HighElves, "Phoenix Guard", "Phoenix Guard");
        await AddCC(FactionKey.Lizardmen, "Temple Guard", "Temple Guard");
        await AddCC(FactionKey.OrcsAndGoblins, "Black Orc", "Black Orcs");
        await AddCC(FactionKey.OrcsAndGoblins, "Big", "Big 'Uns");
        await AddCC(FactionKey.Skaven, "Stormvermin", "Stormvermin");
        await AddCC(FactionKey.Skaven, "Plague Monk", "Plague Monks");
        await AddCC(FactionKey.TheEmpire, "Greatswords", "Greatswords");
        await AddCC(FactionKey.TombKings, "Tomb Guard", "Tomb Guard");
        await AddCC(FactionKey.VampireCounts, "Grave Guard", "Grave Guard");
        await AddCC(FactionKey.WarriorsOfChaos, "Chaos Warriors", "Chaos Warriors");
        await AddCC(FactionKey.WarriorsOfChaos, "Chosen", "Chosen");
        await AddCC(FactionKey.WoodElves, "Wildwood Rangers", "Wildwood Rangers");
        await AddCC(FactionKey.WoodElves, "Wardancers", "Wardancers");

        // Print opponent roster
        _output.WriteLine("DEDICATED CC UNITS ROSTER:");
        _output.WriteLine($"{"Unit",-24} {"Faction",-20} {"WS",3} {"S",2} {"T",2} {"I",2} {"A",2} {"Ld",3} {"Pts",4} {"Save",5} {"Key Rules"}");
        _output.WriteLine(new string('-', 120));
        foreach (var (label, unit, fk) in ccUnits)
        {
            var s = unit.BaseModel.Stats;
            var rules = unit.BaseModel.SpecialRules;
            var rulesSummary = new List<string>();
            if (rules.KillingBlow) rulesSummary.Add("KB");
            if (rules.HeroicKillingBlow) rulesSummary.Add("HKB");
            if (rules.Hatred || rules.EternalHatred) rulesSummary.Add("Hatred");
            if (rules.Frenzy) rulesSummary.Add("Frenzy");
            if (rules.Stubborn) rulesSummary.Add("Stub");
            if (rules.PoisonedAttacks) rulesSummary.Add("Poison");
            if (rules.MightyBlow != null) rulesSummary.Add($"MB({rules.MightyBlow})");
            if (rules.ArmourPiercing != null) rulesSummary.Add($"AP({rules.ArmourPiercing})");
            if (rules.PredatoryFighter) rulesSummary.Add("PF");
            if (rules.ColdBlooded) rulesSummary.Add("CB");
            if (rules.NaturalArmour != null) rulesSummary.Add($"NA({rules.NaturalArmour}+)");
            if (rules.WardSave != null) rulesSummary.Add($"Ward({rules.WardSave}+)");
            if (rules.Regeneration != null) rulesSummary.Add($"Regen({rules.Regeneration}+)");
            if (rules.MartialProwess) rulesSummary.Add("MartProw");
            if (rules.MurderousProwess) rulesSummary.Add("MurdProw");
            if (rules.Swordmaster) rulesSummary.Add("Swordm");
            if (rules.Fear) rulesSummary.Add("Fear");
            if (rules.GromrilArmour) rulesSummary.Add("Gromril");

            _output.WriteLine($"{label,-24} {fk.ToDisplayName(),-20} {s.WS,3} {s.S,2} {s.T,2} {s.I,2} {s.A,2} {s.Ld,3} {unit.BaseModelCost,4:F0} {"?",-5} {string.Join(", ", rulesSummary)}");
        }

        _output.WriteLine("");
        _output.WriteLine($"Subject: Saurus Warriors — WS{saurusUnit.BaseModel.Stats.WS} S{saurusUnit.BaseModel.Stats.S} T{saurusUnit.BaseModel.Stats.T} I{saurusUnit.BaseModel.Stats.I} A{saurusUnit.BaseModel.Stats.A} — {saurusUnit.BaseModelCost}pts");
        _output.WriteLine("Rules: Cold-Blooded, Predatory Fighter, Natural Armour (5+), Shield → 4+ save");

        var buffMod = new UnitModifier
        {
            Label = "WS4/I2",
            Source = "Manual",
            WeaponSkillBonus = 1,
            InitiativeBonus = 1,
        };

        // ═══════════════════════════════════════════════════════
        // SECTION 1: Equal models (25v25)
        // ═══════════════════════════════════════════════════════
        _output.WriteLine("");
        _output.WriteLine(new string('=', 140));
        _output.WriteLine("SECTION 1: EQUAL MODELS (25 vs 25) — raw combat power comparison");
        _output.WriteLine(new string('=', 140));

        PrintGauntletSection(engine, saurusUnit, null, "Base (WS3/I1, 13pts)", 25, ccUnits);
        PrintGauntletSection(engine, saurusUnit, buffMod, "Buff (WS4/I2, 13pts)", 25, ccUnits);

        // ═══════════════════════════════════════════════════════
        // SECTION 2: Points-matched (~375pts budget)
        // ═══════════════════════════════════════════════════════
        _output.WriteLine("");
        _output.WriteLine(new string('=', 140));
        _output.WriteLine("SECTION 2: POINTS-MATCHED (~375pts each side)");
        _output.WriteLine(new string('=', 140));

        var budgets = new[] { 375.0 };
        var pricePoints = new (string Label, double Cost, UnitModifier? Mod)[]
        {
            ("Base Saurus @13pts", 13, null),
            ("Buff Saurus @13pts", 13, buffMod),
            ("Buff Saurus @14pts", 14, buffMod),
            ("Buff Saurus @15pts", 15, buffMod),
        };

        foreach (var budget in budgets)
        {
            _output.WriteLine($"\n  Budget: {budget:F0} pts");
            _output.WriteLine(
                $"  {"Config",-26} {"N",3} | " +
                $"{"Opponent",-20} {"OppN",4} {"Win%",7} {"Loss%",7} {"Draw%",7} {"Dealt",7} {"Taken",7}");
            _output.WriteLine("  " + new string('-', 100));

            foreach (var (ppLabel, ppCost, ppMod) in pricePoints)
            {
                int saurusModels = (int)(budget / ppCost);
                double totalWin = 0, totalLoss = 0, totalDraw = 0;
                double totalDealt = 0, totalTaken = 0;
                int matchups = 0;

                foreach (var (ccLabel, ccUnit, ccFaction) in ccUnits)
                {
                    int oppModels = Math.Max(1, (int)(budget / ccUnit.BaseModelCost));
                    var saurusConfig = new UnitConfiguration(saurusUnit) { ModelCount = saurusModels, Frontage = 5 };
                    if (ppMod != null) saurusConfig.Modifiers.Add(ppMod);
                    var oppConfig = new UnitConfiguration(ccUnit) { ModelCount = oppModels, Frontage = 5 };
                    var ctx = new CombatContext
                    {
                        Charge = ChargeState.Neither,
                        CombatRound = 1,
                        AttackerFactionKey = FactionKey.Lizardmen.ToJsonKey(),
                        DefenderFactionKey = ccFaction.ToJsonKey(),
                    };

                    var result = engine.Simulate(saurusConfig, oppConfig, ctx, 10_000);
                    totalWin += result.AttackerWinPct;
                    totalLoss += result.DefenderWinPct;
                    totalDraw += result.DrawPct;
                    totalDealt += result.AvgAttackerWounds;
                    totalTaken += result.AvgDefenderWounds;
                    matchups++;

                    _output.WriteLine(
                        $"  {ppLabel,-26} {saurusModels,3} | " +
                        $"{ccLabel,-20} {oppModels,4} {result.AttackerWinPct,6:F1}% {result.DefenderWinPct,6:F1}% {result.DrawPct,6:F1}% " +
                        $"{result.AvgAttackerWounds,7:F2} {result.AvgDefenderWounds,7:F2}");
                }

                _output.WriteLine(
                    $"  {ppLabel,-26} {"AVG",-3} | " +
                    $"{"--- AVERAGE ---",-20} {"",4} {totalWin / matchups,6:F1}% {totalLoss / matchups,6:F1}% {totalDraw / matchups,6:F1}% " +
                    $"{totalDealt / matchups,7:F2} {totalTaken / matchups,7:F2}");
                _output.WriteLine("  " + new string('-', 100));
            }
        }

        // ═══════════════════════════════════════════════════════
        // SECTION 3: Cross-compare — each CC unit vs Saurus AND vs each other CC unit's price-peers
        // Show how Saurus compares to other units at the same cost tier
        // ═══════════════════════════════════════════════════════
        _output.WriteLine("");
        _output.WriteLine(new string('=', 140));
        _output.WriteLine("SECTION 3: HOW DO OTHER ~13-14pt CC UNITS PERFORM AGAINST THE SAME OPPONENTS? (points-matched at 375pts)");
        _output.WriteLine(new string('=', 140));

        // Find CC units in the 10-18pt range to compare
        var comparables = ccUnits.Where(c =>
            c.Unit.BaseModelCost >= 8 && c.Unit.BaseModelCost <= 18).ToList();

        _output.WriteLine(
            $"  {"Unit",-24} {"Pts",4} {"N",3} | {"Avg Win%",9} {"Avg Loss%",10} {"Avg Dealt",10} {"Avg Taken",10}");
        _output.WriteLine("  " + new string('-', 80));

        // Base and buffed Saurus first
        foreach (var (label, cost, mod) in new[] { ("Saurus BASE", 13.0, (UnitModifier?)null), ("Saurus BUFF", 14.0, (UnitModifier?)buffMod) })
        {
            int n = (int)(375.0 / cost);
            double tw = 0, tl = 0, tDealt = 0, tTaken = 0;
            int ct = 0;
            foreach (var (_, ccUnit, ccFaction) in ccUnits)
            {
                int oppN = Math.Max(1, (int)(375.0 / ccUnit.BaseModelCost));
                var sc = new UnitConfiguration(saurusUnit) { ModelCount = n, Frontage = 5 };
                if (mod != null) sc.Modifiers.Add(mod);
                var oc = new UnitConfiguration(ccUnit) { ModelCount = oppN, Frontage = 5 };
                var ctx = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1,
                    AttackerFactionKey = FactionKey.Lizardmen.ToJsonKey(), DefenderFactionKey = ccFaction.ToJsonKey() };
                var r = engine.Simulate(sc, oc, ctx, 10_000);
                tw += r.AttackerWinPct; tl += r.DefenderWinPct;
                tDealt += r.AvgAttackerWounds; tTaken += r.AvgDefenderWounds;
                ct++;
            }
            _output.WriteLine($"  {label,-24} {cost,4:F0} {n,3} | {tw/ct,8:F1}% {tl/ct,9:F1}% {tDealt/ct,10:F2} {tTaken/ct,10:F2}");
        }

        // Each comparable CC unit as attacker vs the same opponent list
        foreach (var (compLabel, compUnit, compFaction) in comparables)
        {
            int compN = Math.Max(1, (int)(375.0 / compUnit.BaseModelCost));
            double tw = 0, tl = 0, tDealt = 0, tTaken = 0;
            int ct = 0;
            foreach (var (_, ccUnit, ccFaction) in ccUnits)
            {
                if (ccUnit == compUnit) continue; // skip self-matchup
                int oppN = Math.Max(1, (int)(375.0 / ccUnit.BaseModelCost));
                var sc = new UnitConfiguration(compUnit) { ModelCount = compN, Frontage = 5 };
                var oc = new UnitConfiguration(ccUnit) { ModelCount = oppN, Frontage = 5 };
                var ctx = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1,
                    AttackerFactionKey = compFaction.ToJsonKey(), DefenderFactionKey = ccFaction.ToJsonKey() };
                var r = engine.Simulate(sc, oc, ctx, 10_000);
                tw += r.AttackerWinPct; tl += r.DefenderWinPct;
                tDealt += r.AvgAttackerWounds; tTaken += r.AvgDefenderWounds;
                ct++;
            }
            _output.WriteLine($"  {compLabel,-24} {compUnit.BaseModelCost,4:F0} {compN,3} | {tw/ct,8:F1}% {tl/ct,9:F1}% {tDealt/ct,10:F2} {tTaken/ct,10:F2}");
        }
    }

    private void PrintGauntletSection(
        CombatEngine engine, Unit saurusUnit, UnitModifier? mod, string label, int models,
        List<(string Label, Unit Unit, FactionKey Faction)> opponents)
    {
        _output.WriteLine($"\n  {label}:");
        _output.WriteLine(
            $"  {"Opponent",-24} {"Win%",7} {"Loss%",7} {"Draw%",7} {"Dealt",7} {"Taken",7}");
        _output.WriteLine("  " + new string('-', 60));

        double tw = 0, tl = 0, td = 0, tDealt = 0, tTaken = 0;
        int ct = 0;

        foreach (var (oppLabel, oppUnit, oppFaction) in opponents)
        {
            var sc = new UnitConfiguration(saurusUnit) { ModelCount = models, Frontage = 5 };
            if (mod != null) sc.Modifiers.Add(mod);
            var oc = new UnitConfiguration(oppUnit) { ModelCount = models, Frontage = 5 };
            var ctx = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1,
                AttackerFactionKey = FactionKey.Lizardmen.ToJsonKey(), DefenderFactionKey = oppFaction.ToJsonKey() };
            var r = engine.Simulate(sc, oc, ctx, 10_000);
            tw += r.AttackerWinPct; tl += r.DefenderWinPct; td += r.DrawPct;
            tDealt += r.AvgAttackerWounds; tTaken += r.AvgDefenderWounds;
            ct++;

            _output.WriteLine(
                $"  {oppLabel,-24} {r.AttackerWinPct,6:F1}% {r.DefenderWinPct,6:F1}% {r.DrawPct,6:F1}% " +
                $"{r.AvgAttackerWounds,7:F2} {r.AvgDefenderWounds,7:F2}");
        }
        _output.WriteLine("  " + new string('-', 60));
        _output.WriteLine(
            $"  {"AVERAGE",-24} {tw/ct,6:F1}% {tl/ct,6:F1}% {td/ct,6:F1}% " +
            $"{tDealt/ct,7:F2} {tTaken/ct,7:F2}");
    }

    /// <summary>
    /// Isolate why Chaos Warriors (WS5/I4, 4+ save, no rules) perform similarly to
    /// buffed Saurus (WS4/I2, 4+ save, Predatory Fighter). Uses synthetic units to
    /// control each variable independently.
    /// </summary>
    [Fact]
    public void ChaosWarriorVsSaurus_RuleAndStatIsolation()
    {
        var repo = TestHelpers.CreateRepository();
        var engine = new CombatEngine(repo);

        // Build a representative set of opponents using MakeUnit
        // Covers the spread: chaff, mid-tier, elite
        var opponents = new List<(string Name, Unit Unit)>
        {
            ("Empire Spearmen",    TestHelpers.MakeUnit(name: "Spearmen",    ws: 3, s: 3, t: 3, i: 3, a: 1, ld: 7, points: 5,  equipment: "Hand weapon, shield", specialRules: "Fight in Extra Ranks (1)")),
            ("Orc Boyz",           TestHelpers.MakeUnit(name: "Orc Boyz",    ws: 3, s: 3, t: 4, i: 2, a: 1, ld: 7, points: 5,  equipment: "Hand weapon")),
            ("Dwarf Warriors",     TestHelpers.MakeUnit(name: "Dwarf Warriors", ws: 4, s: 3, t: 4, i: 2, a: 1, ld: 9, points: 8,  equipment: "Hand weapon, heavy armour")),
            ("Gor Herd",           TestHelpers.MakeUnit(name: "Gor Herd",    ws: 4, s: 3, t: 4, i: 3, a: 1, ld: 7, points: 7,  equipment: "Hand weapon")),
            ("Grave Guard",        TestHelpers.MakeUnit(name: "Grave Guard", ws: 3, s: 4, t: 4, i: 3, a: 1, ld: 6, points: 10, equipment: "Hand weapon, medium armour", specialRules: "Killing Blow")),
            ("Bestigor",           TestHelpers.MakeUnit(name: "Bestigor",    ws: 4, s: 4, t: 4, i: 3, a: 1, ld: 7, points: 10, equipment: "Hand weapon, light armour")),
            ("Big 'Uns",           TestHelpers.MakeUnit(name: "Big 'Uns",    ws: 4, s: 4, t: 4, i: 2, a: 1, ld: 7, points: 8,  equipment: "Hand weapon")),
            ("Miners",             TestHelpers.MakeUnit(name: "Miners",      ws: 4, s: 3, t: 4, i: 2, a: 1, ld: 9, points: 9,  equipment: "Great weapon, medium armour")),
            ("Greatswords",        TestHelpers.MakeUnit(name: "Greatswords", ws: 4, s: 3, t: 3, i: 4, a: 1, ld: 8, points: 9,  equipment: "Great weapon, light armour", specialRules: "Stubborn")),
            ("Black Orcs",         TestHelpers.MakeUnit(name: "Black Orcs",  ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14, equipment: "Hand weapon, heavy armour", specialRules: "Mighty Blow (1)")),
            ("Swordmasters",       TestHelpers.MakeUnit(name: "Swordmasters",ws: 6, s: 3, t: 3, i: 6, a: 2, ld: 8, points: 16, equipment: "Great weapon, heavy armour", specialRules: "Martial Prowess")),
            ("Temple Guard",       TestHelpers.MakeUnit(name: "Temple Guard",ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 18, equipment: "Polearm, light armour, shield", specialRules: "Cold-blooded, Predatory Fighter, Natural Armour (5+), Stubborn")),
        };

        // ── Synthetic test subjects — each isolates one variable ──
        var subjects = new List<(string Label, Unit Unit, UnitModifier? Mod)>
        {
            // A: "Naked" Chaos Warrior statline — WS5/I4, heavy armour (5+ save), no rules
            ("CW statline (WS5/I4, 5+ sv)",
                TestHelpers.MakeUnit(name: "CW-like", ws: 5, s: 4, t: 4, i: 4, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, heavy armour"),
                null),

            // B: Buffed Saurus — WS4/I2, shield + Natural Armour 5+ (4+ save), Predatory Fighter + Cold-Blooded
            ("Buff Saurus (WS4/I2, 4+ sv, PF+CB)",
                TestHelpers.MakeUnit(name: "Saurus-like", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Cold-blooded, Predatory Fighter, Natural Armour (5+)"),
                null),

            // C: Saurus statline, NO Predatory Fighter — isolates the rule
            ("Buff Saurus NO PredFighter",
                TestHelpers.MakeUnit(name: "Saurus-noPF", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Cold-blooded, Natural Armour (5+)"),
                null),

            // D: Saurus statline, NO Natural Armour (just shield = 6+ save) — isolates armour advantage
            ("Buff Saurus NO NatArmour (6+ sv)",
                TestHelpers.MakeUnit(name: "Saurus-noNA", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Cold-blooded, Predatory Fighter"),
                null),

            // E: CW statline + shield (4+ save) — equalises armour
            ("CW + shield (WS5/I4, 4+ sv)",
                TestHelpers.MakeUnit(name: "CW-shield", ws: 5, s: 4, t: 4, i: 4, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, heavy armour, shield"),
                null),

            // F: Pure stat comparison — same armour (4+ via shield+NA), NO special rules
            ("WS4/I2 naked (4+ sv, no rules)",
                TestHelpers.MakeUnit(name: "Naked-Saurus", ws: 4, s: 4, t: 4, i: 2, a: 2, ld: 8, points: 14,
                    equipment: "Hand weapon, shield",
                    specialRules: "Natural Armour (5+)"),
                null),
        };

        _output.WriteLine("VARIABLE ISOLATION: Chaos Warriors vs Buffed Saurus at 25 models each");
        _output.WriteLine("Each row is a synthetic unit tested against the same 12 representative opponents.");
        _output.WriteLine(new string('=', 120));

        // Run each subject against all opponents
        _output.WriteLine($"{"Subject",-38} {"Avg Win%",9} {"Avg Loss%",10} {"Avg Dealt",10} {"Avg Taken",10}");
        _output.WriteLine(new string('-', 80));

        var subjectResults = new List<(string Label, double AvgWin, double AvgLoss, double AvgDealt, double AvgTaken)>();

        foreach (var (label, subjectUnit, mod) in subjects)
        {
            double totalWin = 0, totalLoss = 0, totalDealt = 0, totalTaken = 0;

            foreach (var (oppName, oppUnit) in opponents)
            {
                var subjectConfig = new UnitConfiguration(subjectUnit) { ModelCount = 25, Frontage = 5 };
                if (mod != null) subjectConfig.Modifiers.Add(mod);

                var oppConfig = new UnitConfiguration(oppUnit) { ModelCount = 25, Frontage = 5 };
                var context = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };

                var result = engine.Simulate(subjectConfig, oppConfig, context, 10_000);
                totalWin += result.AttackerWinPct;
                totalLoss += result.DefenderWinPct;
                totalDealt += result.AvgAttackerWounds;
                totalTaken += result.AvgDefenderWounds;
            }

            int n = opponents.Count;
            var avgWin = totalWin / n;
            var avgLoss = totalLoss / n;
            var avgDealt = totalDealt / n;
            var avgTaken = totalTaken / n;
            subjectResults.Add((label, avgWin, avgLoss, avgDealt, avgTaken));

            _output.WriteLine($"{label,-38} {avgWin,8:F1}% {avgLoss,9:F1}% {avgDealt,10:F2} {avgTaken,10:F2}");
        }

        _output.WriteLine(new string('=', 120));

        // Compute deltas
        var cw = subjectResults[0];      // A: CW statline
        var saurus = subjectResults[1];   // B: Full buffed Saurus
        var noPF = subjectResults[2];     // C: No Predatory Fighter
        var noNA = subjectResults[3];     // D: No Natural Armour
        var cwShield = subjectResults[4]; // E: CW + shield
        var naked = subjectResults[5];    // F: Naked WS4/I2

        _output.WriteLine("");
        _output.WriteLine("DECOMPOSITION (all deltas in win% percentage points):");
        _output.WriteLine($"  Full Chaos Warrior advantage (A vs B):        {cw.AvgWin - saurus.AvgWin:+0.0;-0.0}pp");
        _output.WriteLine($"  Raw stat gap, same armour (E vs B):           {cwShield.AvgWin - saurus.AvgWin:+0.0;-0.0}pp  (WS5/I4 vs WS4/I2 + PF + CB, both 4+ save)");
        _output.WriteLine($"  Value of Predatory Fighter (B vs C):          {saurus.AvgWin - noPF.AvgWin:+0.0;-0.0}pp");
        _output.WriteLine($"  Value of Natural Armour 5+ (B vs D):          {saurus.AvgWin - noNA.AvgWin:+0.0;-0.0}pp");
        _output.WriteLine($"  Value of BOTH rules combined (B vs F):        {saurus.AvgWin - naked.AvgWin:+0.0;-0.0}pp");
        _output.WriteLine($"  Armour gap effect (A vs E):                   {cw.AvgWin - cwShield.AvgWin:+0.0;-0.0}pp  (heavy armour 5+ vs heavy armour+shield 4+)");
        _output.WriteLine($"  Pure stat gap, equal armour, no rules (E vs F): {cwShield.AvgWin - naked.AvgWin:+0.0;-0.0}pp  (WS5/I4 vs WS4/I2)");

        // Per-opponent breakdown
        _output.WriteLine("");
        _output.WriteLine("PER-OPPONENT HEAD-TO-HEAD (CW statline vs Buffed Saurus, 25 models each):");
        _output.WriteLine($"{"Opponent",-18} {"CW Win%",9} {"Saurus Win%",12} {"Gap",6}");
        _output.WriteLine(new string('-', 50));

        foreach (var (oppName, oppUnit) in opponents)
        {
            var cwConfig = new UnitConfiguration(subjects[0].Unit) { ModelCount = 25, Frontage = 5 };
            var saurusConfig = new UnitConfiguration(subjects[1].Unit) { ModelCount = 25, Frontage = 5 };
            var oppConfig1 = new UnitConfiguration(oppUnit) { ModelCount = 25, Frontage = 5 };
            var oppConfig2 = new UnitConfiguration(oppUnit) { ModelCount = 25, Frontage = 5 };
            var ctx = new CombatContext { Charge = ChargeState.Neither, CombatRound = 1 };

            var cwResult = engine.Simulate(cwConfig, oppConfig1, ctx, 10_000);
            var saurusResult = engine.Simulate(saurusConfig, oppConfig2, ctx, 10_000);
            double gap = cwResult.AttackerWinPct - saurusResult.AttackerWinPct;

            _output.WriteLine($"{oppName,-18} {cwResult.AttackerWinPct,8:F1}% {saurusResult.AttackerWinPct,11:F1}% {gap,+5:+0.0;-0.0}");
        }
    }

    /// <summary>
    /// Build a GameDataRepository that can load faction data from the wwwroot/data directory on disk.
    /// </summary>
    private static GameDataRepository CreateFullRepository() => TestHelpers.CreateRepository();
}
