using Warhammer.Calculator.Models;

namespace Warhammer.Tests;

public class CombatUnitStateTests
{
    // ─── ApplyCasualties — W=1 models ────────────────────────────────

    [Fact]
    public void ApplyCasualties_SingleWound_KillsCorrectModels()
    {
        var state = new CombatUnitState(10, woundsPerModel: 1);

        int killed = state.ApplyCasualties(3);

        Assert.Equal(3, killed);
        Assert.Equal(7, state.CurrentModelCount);
    }

    [Fact]
    public void ApplyCasualties_SingleWound_CapsAtModelCount()
    {
        var state = new CombatUnitState(5, woundsPerModel: 1);

        int killed = state.ApplyCasualties(8);

        Assert.Equal(5, killed);
        Assert.Equal(0, state.CurrentModelCount);
    }

    [Fact]
    public void ApplyCasualties_ZeroWounds_NoEffect()
    {
        var state = new CombatUnitState(10, woundsPerModel: 1);

        int killed = state.ApplyCasualties(0);

        Assert.Equal(0, killed);
        Assert.Equal(10, state.CurrentModelCount);
    }

    // ─── ApplyCasualties — Multi-wound models ────────────────────────

    [Fact]
    public void ApplyCasualties_MultiWound_TracksPartialDamage()
    {
        var state = new CombatUnitState(5, woundsPerModel: 3);

        int killed = state.ApplyCasualties(2);

        Assert.Equal(0, killed);
        Assert.Equal(5, state.CurrentModelCount);
        Assert.Equal(1, state.WoundsOnPartialModel); // 3 - 2 = 1 remaining
    }

    [Fact]
    public void ApplyCasualties_MultiWound_KillsAndCarriesOver()
    {
        var state = new CombatUnitState(5, woundsPerModel: 3);

        // 7 wounds: kills 2 models (6 wounds) + 1 wound on third model
        int killed = state.ApplyCasualties(7);

        Assert.Equal(2, killed);
        Assert.Equal(3, state.CurrentModelCount);
        Assert.Equal(2, state.WoundsOnPartialModel); // 3 - 1 = 2 remaining
    }

    [Fact]
    public void ApplyCasualties_MultiWound_ExactKill()
    {
        var state = new CombatUnitState(3, woundsPerModel: 3);

        // Exactly 9 wounds kills all 3 models
        int killed = state.ApplyCasualties(9);

        Assert.Equal(3, killed);
        Assert.Equal(0, state.CurrentModelCount);
    }

    [Fact]
    public void ApplyCasualties_MultiWound_OverkillCapped()
    {
        var state = new CombatUnitState(2, woundsPerModel: 3);

        // 10 wounds vs 6 total HP — only 2 models can die
        int killed = state.ApplyCasualties(10);

        Assert.Equal(2, killed);
        Assert.Equal(0, state.CurrentModelCount);
    }

    [Fact]
    public void ApplyCasualties_MultiWound_SequentialDamage()
    {
        var state = new CombatUnitState(3, woundsPerModel: 3);

        // First batch: 2 wounds → partial damage
        state.ApplyCasualties(2);
        Assert.Equal(3, state.CurrentModelCount);
        Assert.Equal(1, state.WoundsOnPartialModel);

        // Second batch: 2 wounds → finishes first model (1) + 1 on next
        int killed = state.ApplyCasualties(2);
        Assert.Equal(1, killed);
        Assert.Equal(2, state.CurrentModelCount);
        Assert.Equal(2, state.WoundsOnPartialModel);
    }

    // ─── TotalRemainingHP ────────────────────────────────────────────

    [Fact]
    public void TotalRemainingHP_FullHealth()
    {
        var state = new CombatUnitState(5, woundsPerModel: 3);

        Assert.Equal(15, state.TotalRemainingHP);
    }

    [Fact]
    public void TotalRemainingHP_PartialDamage()
    {
        var state = new CombatUnitState(5, woundsPerModel: 3);
        state.ApplyCasualties(4); // kills 1 model, 2 wounds on next

        Assert.Equal(11, state.TotalRemainingHP); // 3*3 + 2 = 11
    }

    [Fact]
    public void TotalRemainingHP_AllDead()
    {
        var state = new CombatUnitState(2, woundsPerModel: 1);
        state.ApplyCasualties(2);

        Assert.Equal(0, state.TotalRemainingHP);
    }

    // ─── Reset ───────────────────────────────────────────────────────

    [Fact]
    public void Reset_RestoresOriginalState()
    {
        var state = new CombatUnitState(10, woundsPerModel: 3);
        state.ApplyCasualties(7);

        state.Reset();

        Assert.Equal(10, state.CurrentModelCount);
        Assert.Equal(3, state.WoundsOnPartialModel);
        Assert.Equal(30, state.TotalRemainingHP);
    }
}
