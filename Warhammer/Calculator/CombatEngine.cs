using System.Diagnostics;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class CombatEngine
{
    private readonly IGameDataRepository _repo;

    public CombatEngine(IGameDataRepository repo)
    {
        _repo = repo;
    }

    public CombatSimulationResult Simulate(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        int iterations = 10_000)
    {
        var sw = Stopwatch.StartNew();
        var resolver = new CombatResolver(_repo);
        var rng = new Random();

        // Pre-build attack groups once (weapon parsing, parry calc, sorting)
        var groups = resolver.PrepareAttackGroups(attacker, defender, context);

        int attackerWins = 0;
        int defenderWins = 0;
        int draws = 0;

        long totalAttackerWounds = 0;
        long totalDefenderWounds = 0;
        long totalAttackerCR = 0;
        long totalDefenderCR = 0;

        double sumAttWounds = 0, sumAttRank = 0, sumAttStd = 0, sumAttCharge = 0, sumAttFear = 0, sumAttMusician = 0;
        double sumDefWounds = 0, sumDefRank = 0, sumDefStd = 0, sumDefCharge = 0, sumDefFear = 0, sumDefMusician = 0;

        var distribution = new Dictionary<int, int>();
        var attackerWoundDist = new Dictionary<int, int>();
        var defenderWoundDist = new Dictionary<int, int>();

        for (int i = 0; i < iterations; i++)
        {
            var result = resolver.ResolveRound(attacker, defender, context, groups, rng);

            totalAttackerWounds += result.AttackerWoundsDealt;
            totalDefenderWounds += result.DefenderWoundsDealt;
            totalAttackerCR += result.AttackerCombatRes;
            totalDefenderCR += result.DefenderCombatRes;

            sumAttWounds += result.AttackerBreakdown.Wounds;
            sumAttRank += result.AttackerBreakdown.RankBonus;
            sumAttStd += result.AttackerBreakdown.StandardBearer;
            sumAttCharge += result.AttackerBreakdown.ChargeBonus;
            sumAttFear += result.AttackerBreakdown.FearBonus;
            sumAttMusician += result.AttackerBreakdown.MusicianBonus;

            sumDefWounds += result.DefenderBreakdown.Wounds;
            sumDefRank += result.DefenderBreakdown.RankBonus;
            sumDefStd += result.DefenderBreakdown.StandardBearer;
            sumDefCharge += result.DefenderBreakdown.ChargeBonus;
            sumDefFear += result.DefenderBreakdown.FearBonus;
            sumDefMusician += result.DefenderBreakdown.MusicianBonus;

            int diff = result.AttackerCombatRes - result.DefenderCombatRes;
            if (diff > 0) attackerWins++;
            else if (diff < 0) defenderWins++;
            else draws++;

            distribution[diff] = distribution.GetValueOrDefault(diff) + 1;
            attackerWoundDist[result.AttackerWoundsDealt] = attackerWoundDist.GetValueOrDefault(result.AttackerWoundsDealt) + 1;
            defenderWoundDist[result.DefenderWoundsDealt] = defenderWoundDist.GetValueOrDefault(result.DefenderWoundsDealt) + 1;
        }

        sw.Stop();
        double n = iterations;

        return new CombatSimulationResult
        {
            Iterations = iterations,
            ElapsedMs = sw.ElapsedMilliseconds,
            AttackerWinPct = Math.Round(attackerWins / n * 100, 1),
            DefenderWinPct = Math.Round(defenderWins / n * 100, 1),
            DrawPct = Math.Round(draws / n * 100, 1),
            AvgAttackerWounds = Math.Round(totalAttackerWounds / n, 2),
            AvgDefenderWounds = Math.Round(totalDefenderWounds / n, 2),
            AvgAttackerCombatRes = Math.Round(totalAttackerCR / n, 2),
            AvgDefenderCombatRes = Math.Round(totalDefenderCR / n, 2),
            AvgAttackerBreakdown = new CombatResBreakdown
            {
                Wounds = Math.Round(sumAttWounds / n, 2),
                RankBonus = Math.Round(sumAttRank / n, 2),
                StandardBearer = Math.Round(sumAttStd / n, 2),
                ChargeBonus = Math.Round(sumAttCharge / n, 2),
                FearBonus = Math.Round(sumAttFear / n, 2),
                MusicianBonus = Math.Round(sumAttMusician / n, 2)
            },
            AvgDefenderBreakdown = new CombatResBreakdown
            {
                Wounds = Math.Round(sumDefWounds / n, 2),
                RankBonus = Math.Round(sumDefRank / n, 2),
                StandardBearer = Math.Round(sumDefStd / n, 2),
                ChargeBonus = Math.Round(sumDefCharge / n, 2),
                FearBonus = Math.Round(sumDefFear / n, 2),
                MusicianBonus = Math.Round(sumDefMusician / n, 2)
            },
            Distribution = distribution
                .OrderBy(kv => kv.Key)
                .Select(kv => new DistributionEntry
                {
                    CombatResDifference = kv.Key,
                    Count = kv.Value,
                    Percentage = Math.Round(kv.Value / n * 100, 1)
                })
                .ToList(),
            AttackerWoundDistribution = attackerWoundDist
                .OrderBy(kv => kv.Key)
                .Select(kv => new WoundDistributionEntry
                {
                    Wounds = kv.Key,
                    Count = kv.Value,
                    Percentage = Math.Round(kv.Value / n * 100, 1)
                })
                .ToList(),
            DefenderWoundDistribution = defenderWoundDist
                .OrderBy(kv => kv.Key)
                .Select(kv => new WoundDistributionEntry
                {
                    Wounds = kv.Key,
                    Count = kv.Value,
                    Percentage = Math.Round(kv.Value / n * 100, 1)
                })
                .ToList()
        };
    }
}
