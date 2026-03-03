using Warhammer.Models.Domain;

namespace Warhammer.Calculator;

public static class BaseContactCalculator
{
    /// <summary>
    /// Calculate how many models of 'unit' can be in base contact with 'opponent'.
    /// WAP formula: min(unitFiles, ceil(opponentWidthMm / unitBaseWidthMm) + 2)
    /// The +2 accounts for corner-to-corner contact on both sides.
    /// </summary>
    public static int ModelsInBaseContact(UnitConfiguration unit, UnitConfiguration opponent)
    {
        int unitFrontage = unit.ModelsInFrontRank;
        int opponentWidthMm = opponent.ModelsInFrontRank * opponent.BaseWidthMm;
        int unitBaseWidthMm = Math.Max(1, unit.BaseWidthMm);

        int fittable = (int)Math.Ceiling((double)opponentWidthMm / unitBaseWidthMm) + 2;
        return Math.Min(unitFrontage, fittable);
    }
}
