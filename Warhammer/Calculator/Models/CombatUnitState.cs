namespace Warhammer.Calculator.Models;

/// <summary>
/// Mutable state tracking casualties for a single unit during and between combat rounds.
/// Stack-allocated struct for zero GC pressure in the hot simulation loop.
/// </summary>
public struct CombatUnitState
{
    public int CurrentModelCount;
    public int WoundsOnPartialModel;
    public int WoundsPerModel;
    public int OriginalModelCount;

    public CombatUnitState(int modelCount, int woundsPerModel)
    {
        CurrentModelCount = modelCount;
        OriginalModelCount = modelCount;
        WoundsPerModel = woundsPerModel;
        WoundsOnPartialModel = woundsPerModel;
    }

    /// <summary>
    /// Apply wounds, reducing model count as models die.
    /// For multi-wound models, tracks partial damage on the front model.
    /// Returns the number of models killed.
    /// </summary>
    public int ApplyCasualties(int wounds)
    {
        if (wounds <= 0 || CurrentModelCount <= 0) return 0;

        int modelsKilled = 0;

        if (WoundsPerModel == 1)
        {
            modelsKilled = Math.Min(wounds, CurrentModelCount);
            CurrentModelCount -= modelsKilled;
            return modelsKilled;
        }

        int remaining = wounds;
        while (remaining > 0 && CurrentModelCount > 0)
        {
            if (remaining >= WoundsOnPartialModel)
            {
                remaining -= WoundsOnPartialModel;
                CurrentModelCount--;
                modelsKilled++;
                WoundsOnPartialModel = WoundsPerModel;
            }
            else
            {
                WoundsOnPartialModel -= remaining;
                remaining = 0;
            }
        }

        return modelsKilled;
    }

    /// <summary>Total remaining wound points across all models.</summary>
    public readonly int TotalRemainingHP =>
        CurrentModelCount > 0
            ? (CurrentModelCount - 1) * WoundsPerModel + WoundsOnPartialModel
            : 0;

    /// <summary>
    /// Reset to original state for the next iteration.
    /// </summary>
    public void Reset()
    {
        CurrentModelCount = OriginalModelCount;
        WoundsOnPartialModel = WoundsPerModel;
    }
}
