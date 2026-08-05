using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

namespace Warhammer.Tests;

/// <summary>
/// The gauntlet table reports combat resolution, not the multi-round outcome buckets, because
/// a unit that can neither break nor be wiped out inside the cap lands in MaxRoundsReached
/// however far ahead the subject was.
/// </summary>
public class GauntletVerdictTests
{
    private static readonly CombatContext Context = new()
    {
        Charge = ChargeState.Neither,
        CombatRound = 1,
        AttackerFactionKey = "lizardmen",
        DefenderFactionKey = "vampire-counts",
    };

    private static async Task<(UnitConfiguration Saurus, UnitConfiguration Skeletons)> Matchup()
    {
        var repo = TestHelpers.Repo;
        var lizardmen = await repo.GetUnitsAsync(FactionKey.Lizardmen);
        var vc = await repo.GetUnitsAsync(FactionKey.VampireCounts);

        var saurus = new UnitConfiguration(
            lizardmen.First(u => u.Name == "Saurus Warriors")) { ModelCount = 25 };
        var skeletons = new UnitConfiguration(
            vc.First(u => u.Name == "Skeleton Warriors")) { ModelCount = 40 };
        return (saurus, skeletons);
    }

    [Fact]
    public async Task TheCombatResVerdictDoesNotMoveWithTheRoundCap()
    {
        var (saurus, skeletons) = await Matchup();
        var engine = new CombatEngine(TestHelpers.Repo);

        // A deeper run draws more dice per iteration, so the shared RNG lands elsewhere and the
        // figures differ by sampling noise — but only by that. The old outcome-bucket verdict
        // moved by more than sixty points across this same pair of caps.
        var shortRun = new GauntletMatchup(skeletons.ResolvedUnit, skeletons, "vampire-counts", null,
            engine.SimulateMultiRound(saurus, skeletons, Context, maxRounds: 3, iterations: 8000, seed: 5));
        var longRun = new GauntletMatchup(skeletons.ResolvedUnit, skeletons, "vampire-counts", null,
            engine.SimulateMultiRound(saurus, skeletons, Context, maxRounds: 8, iterations: 8000, seed: 5));

        Assert.Equal(shortRun.WinPct, longRun.WinPct, 1.5);
        Assert.Equal(shortRun.AvgCombatRes, longRun.AvgCombatRes, 0.2);
        Assert.Equal(shortRun.AvgOpponentCombatRes, longRun.AvgOpponentCombatRes, 0.2);

        double oldShort = shortRun.DecisiveWinPct ?? 0;
        double oldLong = longRun.DecisiveWinPct ?? 0;
        Assert.True(oldLong - oldShort > 60,
            $"the cap-bound reading should still swing wildly ({oldShort:F1}% -> {oldLong:F1}%), "
            + "otherwise this test isn't measuring what it claims");
    }

    [Fact]
    public async Task AGrindThatOutrunsTheCapStillReadsAsAWin()
    {
        // 25 Saurus against 40 Unstable skeletons: ahead on combat resolution in every round,
        // but too many bodies to clear in three. The outcome buckets call that a draw.
        var (saurus, skeletons) = await Matchup();
        var engine = new CombatEngine(TestHelpers.Repo);

        var result = engine.SimulateMultiRound(saurus, skeletons, Context, maxRounds: 3, iterations: 4000, seed: 5);
        var matchup = new GauntletMatchup(skeletons.ResolvedUnit, skeletons, "vampire-counts", null, result);

        Assert.True(matchup.CombatResMargin > 3,
            $"Saurus should be well ahead on CR, margin was {matchup.CombatResMargin:F2}");
        Assert.True(matchup.WinPct > 90, $"CR win rate was {matchup.WinPct:F1}%");
        Assert.True(matchup.LossPct < 5, $"CR loss rate was {matchup.LossPct:F1}%");

        // The cap-bound outcome the old columns reported, kept but no longer the headline.
        Assert.True(result.OutcomePcts.GetValueOrDefault(CombatOutcome.MaxRoundsReached) > 50,
            "the matchup should still be unresolved at the cap — otherwise this test proves nothing");
    }

    [Fact]
    public async Task SingleAndMultiRoundAgreeOnTheFirstRound()
    {
        var (saurus, skeletons) = await Matchup();
        var engine = new CombatEngine(TestHelpers.Repo);

        var single = new GauntletMatchup(skeletons.ResolvedUnit, skeletons, "vampire-counts",
            engine.Simulate(saurus, skeletons, Context, iterations: 6000, seed: 9), null);
        var multi = new GauntletMatchup(skeletons.ResolvedUnit, skeletons, "vampire-counts", null,
            engine.SimulateMultiRound(saurus, skeletons, Context, maxRounds: 4, iterations: 6000, seed: 9));

        // Same measurement on the same opening round, so they should land within noise.
        Assert.Equal(single.WinPct, multi.WinPct, 1.5);
        Assert.Equal(single.AvgCombatRes, multi.AvgCombatRes, 0.3);
        Assert.Equal(single.AvgOpponentCombatRes, multi.AvgOpponentCombatRes, 0.3);
    }
}
