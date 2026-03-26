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

    /// <summary>
    /// Simulate multiple rounds of combat per iteration, carrying casualties forward.
    /// Combat ends when one side is destroyed or the max round cap is reached.
    /// Break tests and flee/pursue are not yet implemented (Phases 2-3).
    /// </summary>
    public MultiRoundSimulationResult SimulateMultiRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        int maxRounds = 6,
        int iterations = 10_000)
    {
        var sw = Stopwatch.StartNew();
        var resolver = new CombatResolver(_repo);
        var rng = new Random();

        // Pre-build attack groups once — they reference attacker/defender objects,
        // and computed properties (TotalAttacks, Ranks, etc.) read ModelCount dynamically.
        var groups = resolver.PrepareAttackGroups(attacker, defender, context);

        int origAttModels = attacker.ModelCount;
        int origDefModels = defender.ModelCount;
        int attW = attacker.PrimaryProfile.W ?? 1;
        int defW = defender.PrimaryProfile.W ?? 1;

        var outcomeCounts = new Dictionary<CombatOutcome, int>();
        long totalRoundsPlayed = 0;

        // Per-round accumulators (index 0 = round 1)
        // Every iteration contributes to every round — if combat ended early,
        // remaining rounds record the final state with zero wounds/CR.
        var roundOngoing = new int[maxRounds]; // iterations where combat was still active this round
        var roundAttModelsStartSum = new long[maxRounds]; // models at START of round
        var roundAttModelsEndSum = new long[maxRounds];   // models at END of round
        var roundDefModelsStartSum = new long[maxRounds];
        var roundDefModelsEndSum = new long[maxRounds];
        var roundAttWins = new int[maxRounds];
        var roundDefWins = new int[maxRounds];
        var roundDraws = new int[maxRounds];
        var roundTotalAttWounds = new long[maxRounds];
        var roundTotalDefWounds = new long[maxRounds];
        var roundTotalAttCR = new long[maxRounds];
        var roundTotalDefCR = new long[maxRounds];
        var roundSumAttBreakdown = new double[maxRounds, 6]; // Wounds,Rank,Std,Charge,Fear,Musician
        var roundSumDefBreakdown = new double[maxRounds, 6];

        // Per-round break test accumulators
        var roundAttTested = new int[maxRounds];
        var roundAttFled = new int[maxRounds];
        var roundDefTested = new int[maxRounds];
        var roundDefFled = new int[maxRounds];

        // Per-round distribution accumulators
        var roundCRDist = new Dictionary<int, int>[maxRounds];
        var roundAttWoundDist = new Dictionary<int, int>[maxRounds];
        var roundDefWoundDist = new Dictionary<int, int>[maxRounds];
        for (int r = 0; r < maxRounds; r++)
        {
            roundCRDist[r] = new Dictionary<int, int>();
            roundAttWoundDist[r] = new Dictionary<int, int>();
            roundDefWoundDist[r] = new Dictionary<int, int>();
        }

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                var attackerState = new CombatUnitState(origAttModels, attW);
                var defenderState = new CombatUnitState(origDefModels, defW);

                CombatOutcome outcome = CombatOutcome.MaxRoundsReached;
                bool outcomeDecided = false;
                int roundOfOutcome = maxRounds;

                // Every iteration always runs all rounds — no early termination.
                // Outcome is recorded on the first qualifying event.
                // If one side reaches 0 models, subsequent rounds naturally produce 0 attacks.
                for (int round = 1; round <= maxRounds; round++)
                {
                    int ri = round - 1;

                    roundAttModelsStartSum[ri] += attackerState.CurrentModelCount;
                    roundDefModelsStartSum[ri] += defenderState.CurrentModelCount;

                    attacker.ModelCount = attackerState.CurrentModelCount;
                    defender.ModelCount = defenderState.CurrentModelCount;

                    var roundContext = round == 1
                        ? context
                        : context with { CombatRound = round, Charge = ChargeState.Neither };

                    var result = resolver.ResolveRound(attacker, defender, roundContext, groups, rng,
                        ref attackerState, ref defenderState);

                    roundOngoing[ri]++;

                    // Accumulate per-round stats
                    roundAttModelsEndSum[ri] += attackerState.CurrentModelCount;
                    roundDefModelsEndSum[ri] += defenderState.CurrentModelCount;
                    roundTotalAttWounds[ri] += result.AttackerWoundsDealt;
                    roundTotalDefWounds[ri] += result.DefenderWoundsDealt;
                    roundTotalAttCR[ri] += result.AttackerCombatRes;
                    roundTotalDefCR[ri] += result.DefenderCombatRes;

                    int diff = result.AttackerCombatRes - result.DefenderCombatRes;
                    if (diff > 0) roundAttWins[ri]++;
                    else if (diff < 0) roundDefWins[ri]++;
                    else roundDraws[ri]++;

                    roundSumAttBreakdown[ri, 0] += result.AttackerBreakdown.Wounds;
                    roundSumAttBreakdown[ri, 1] += result.AttackerBreakdown.RankBonus;
                    roundSumAttBreakdown[ri, 2] += result.AttackerBreakdown.StandardBearer;
                    roundSumAttBreakdown[ri, 3] += result.AttackerBreakdown.ChargeBonus;
                    roundSumAttBreakdown[ri, 4] += result.AttackerBreakdown.FearBonus;
                    roundSumAttBreakdown[ri, 5] += result.AttackerBreakdown.MusicianBonus;

                    roundSumDefBreakdown[ri, 0] += result.DefenderBreakdown.Wounds;
                    roundSumDefBreakdown[ri, 1] += result.DefenderBreakdown.RankBonus;
                    roundSumDefBreakdown[ri, 2] += result.DefenderBreakdown.StandardBearer;
                    roundSumDefBreakdown[ri, 3] += result.DefenderBreakdown.ChargeBonus;
                    roundSumDefBreakdown[ri, 4] += result.DefenderBreakdown.FearBonus;
                    roundSumDefBreakdown[ri, 5] += result.DefenderBreakdown.MusicianBonus;

                    roundCRDist[ri][diff] = roundCRDist[ri].GetValueOrDefault(diff) + 1;
                    roundAttWoundDist[ri][result.AttackerWoundsDealt] = roundAttWoundDist[ri].GetValueOrDefault(result.AttackerWoundsDealt) + 1;
                    roundDefWoundDist[ri][result.DefenderWoundsDealt] = roundDefWoundDist[ri].GetValueOrDefault(result.DefenderWoundsDealt) + 1;

                    // Record outcome on first qualifying event (but don't stop)
                    if (!outcomeDecided)
                    {
                        bool attDead = attackerState.CurrentModelCount <= 0;
                        bool defDead = defenderState.CurrentModelCount <= 0;

                        if (attDead && defDead) { outcome = CombatOutcome.BothWipedOut; outcomeDecided = true; roundOfOutcome = round; }
                        else if (attDead) { outcome = CombatOutcome.AttackerWipedOut; outcomeDecided = true; roundOfOutcome = round; }
                        else if (defDead) { outcome = CombatOutcome.DefenderWipedOut; outcomeDecided = true; roundOfOutcome = round; }
                    }

                    // Break test — always resolved for stats, even after outcome is decided.
                    // Only both-sides-alive iterations take break tests.
                    if (attackerState.CurrentModelCount > 0 && defenderState.CurrentModelCount > 0)
                    {
                        int crDiff = result.AttackerCombatRes - result.DefenderCombatRes;
                        if (crDiff != 0)
                        {
                            bool attackerLost = crDiff < 0;
                            var loserState = attackerLost ? ref attackerState : ref defenderState;
                            var loserUnit = attackerLost ? attacker : defender;
                            int loserRanks = loserState.CurrentModelCount / loserUnit.Frontage;
                            var winnerUnit = attackerLost ? defender : attacker;
                            var winnerState = attackerLost ? ref defenderState : ref attackerState;
                            int winnerRanks = winnerState.CurrentModelCount / winnerUnit.Frontage;

                            // Track who was tested
                            if (attackerLost) roundAttTested[ri]++;
                            else roundDefTested[ri]++;

                            var breakResult = BreakTestResolver.Resolve(
                                Math.Abs(crDiff),
                                loserUnit.PrimaryProfile.Ld ?? 7,
                                loserUnit.EffectiveSpecialRules,
                                loserRanks,
                                winnerRanks,
                                rng);

                            switch (breakResult.Outcome)
                            {
                                case BreakTestOutcome.Failed:
                                {
                                    // Track who fled
                                    if (attackerLost) roundAttFled[ri]++;
                                    else roundDefFled[ri]++;

                                    if (!outcomeDecided)
                                    {
                                        var loserRules = loserUnit.EffectiveSpecialRules;
                                        var winnerRules = (attackerLost ? defender : attacker).EffectiveSpecialRules;
                                        bool fleerSwift = loserRules.Swiftstride || loserUnit.TroopType.HasInherentSwiftstride;
                                        bool pursuerSwift = winnerRules.Swiftstride
                                            || (attackerLost ? defender : attacker).TroopType.HasInherentSwiftstride;

                                        var fleeResult = FleeAndPursueResolver.Resolve(fleerSwift, pursuerSwift, rng);

                                        outcome = (attackerLost, fleeResult) switch
                                        {
                                            (true, FleeResult.Caught) => CombatOutcome.AttackerBrokeCaught,
                                            (true, _) => CombatOutcome.AttackerBrokeEscaped,
                                            (false, FleeResult.Caught) => CombatOutcome.DefenderBrokeCaught,
                                            _ => CombatOutcome.DefenderBrokeEscaped
                                        };
                                        outcomeDecided = true; roundOfOutcome = round;
                                    }
                                    break;
                                }

                                case BreakTestOutcome.UnstableWounds:
                                    if (breakResult.UnstableWoundsDealt > 0)
                                    {
                                        int cappedUnstable = Math.Min(
                                            breakResult.UnstableWoundsDealt,
                                            loserState.TotalRemainingHP);
                                        loserState.ApplyCasualties(cappedUnstable);

                                        if (!outcomeDecided && loserState.CurrentModelCount <= 0)
                                        {
                                            outcome = attackerLost
                                                ? CombatOutcome.AttackerWipedOut
                                                : CombatOutcome.DefenderWipedOut;
                                            outcomeDecided = true; roundOfOutcome = round;
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }

                totalRoundsPlayed += roundOfOutcome;
                outcomeCounts[outcome] = outcomeCounts.GetValueOrDefault(outcome) + 1;
            }
        }
        finally
        {
            attacker.ModelCount = origAttModels;
            defender.ModelCount = origDefModels;
        }

        sw.Stop();
        double n = iterations;

        // Build per-round summaries — every round always has n iterations.
        var roundSummaries = new List<RoundSummary>(maxRounds);
        for (int r = 0; r < maxRounds; r++)
        {
            roundSummaries.Add(new RoundSummary
            {
                Round = r + 1,
                IterationsReached = iterations,
                PctStillFighting = Math.Round(roundOngoing[r] / n * 100, 1),
                AvgAttackerModelsStart = Math.Round(roundAttModelsStartSum[r] / n, 2),
                AvgDefenderModelsStart = Math.Round(roundDefModelsStartSum[r] / n, 2),
                AvgAttackerModelsRemaining = Math.Round(roundAttModelsEndSum[r] / n, 2),
                AvgDefenderModelsRemaining = Math.Round(roundDefModelsEndSum[r] / n, 2),
                AttackerTestedPct = Math.Round(roundAttTested[r] / n * 100, 1),
                AttackerFledPct = Math.Round(roundAttFled[r] / n * 100, 1),
                DefenderTestedPct = Math.Round(roundDefTested[r] / n * 100, 1),
                DefenderFledPct = Math.Round(roundDefFled[r] / n * 100, 1),
                AttackerWinPct = Math.Round(roundAttWins[r] / n * 100, 1),
                DefenderWinPct = Math.Round(roundDefWins[r] / n * 100, 1),
                DrawPct = Math.Round(roundDraws[r] / n * 100, 1),
                AvgAttackerWounds = Math.Round(roundTotalAttWounds[r] / n, 2),
                AvgDefenderWounds = Math.Round(roundTotalDefWounds[r] / n, 2),
                AvgAttackerCombatRes = Math.Round(roundTotalAttCR[r] / n, 2),
                AvgDefenderCombatRes = Math.Round(roundTotalDefCR[r] / n, 2),
                AvgAttackerBreakdown = new CombatResBreakdown
                {
                    Wounds = Math.Round(roundSumAttBreakdown[r, 0] / n, 2),
                    RankBonus = Math.Round(roundSumAttBreakdown[r, 1] / n, 2),
                    StandardBearer = Math.Round(roundSumAttBreakdown[r, 2] / n, 2),
                    ChargeBonus = Math.Round(roundSumAttBreakdown[r, 3] / n, 2),
                    FearBonus = Math.Round(roundSumAttBreakdown[r, 4] / n, 2),
                    MusicianBonus = Math.Round(roundSumAttBreakdown[r, 5] / n, 2)
                },
                AvgDefenderBreakdown = new CombatResBreakdown
                {
                    Wounds = Math.Round(roundSumDefBreakdown[r, 0] / n, 2),
                    RankBonus = Math.Round(roundSumDefBreakdown[r, 1] / n, 2),
                    StandardBearer = Math.Round(roundSumDefBreakdown[r, 2] / n, 2),
                    ChargeBonus = Math.Round(roundSumDefBreakdown[r, 3] / n, 2),
                    FearBonus = Math.Round(roundSumDefBreakdown[r, 4] / n, 2),
                    MusicianBonus = Math.Round(roundSumDefBreakdown[r, 5] / n, 2)
                },
                Distribution = roundCRDist[r]
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new DistributionEntry
                    {
                        CombatResDifference = kv.Key,
                        Count = kv.Value,
                        Percentage = Math.Round(kv.Value / n * 100, 1)
                    })
                    .ToList(),
                AttackerWoundDistribution = roundAttWoundDist[r]
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new WoundDistributionEntry
                    {
                        Wounds = kv.Key,
                        Count = kv.Value,
                        Percentage = Math.Round(kv.Value / n * 100, 1)
                    })
                    .ToList(),
                DefenderWoundDistribution = roundDefWoundDist[r]
                    .OrderBy(kv => kv.Key)
                    .Select(kv => new WoundDistributionEntry
                    {
                        Wounds = kv.Key,
                        Count = kv.Value,
                        Percentage = Math.Round(kv.Value / n * 100, 1)
                    })
                    .ToList()
            });
        }

        // Build outcome percentages
        var outcomePcts = new Dictionary<CombatOutcome, double>();
        foreach (var (outcome, count) in outcomeCounts)
            outcomePcts[outcome] = Math.Round(count / n * 100, 1);

        return new MultiRoundSimulationResult
        {
            Iterations = iterations,
            ElapsedMs = sw.ElapsedMilliseconds,
            MaxRounds = maxRounds,
            AvgRoundsPlayed = Math.Round(totalRoundsPlayed / n, 2),
            OutcomePcts = outcomePcts,
            RoundSummaries = roundSummaries
        };
    }
}
