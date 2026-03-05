namespace Warhammer.Calculator.Models;

public enum ChargeState { Neither, AttackerCharged, DefenderCharged }

public record CombatContext
{
    public ChargeState Charge { get; init; } = ChargeState.Neither;
    public int CombatRound { get; init; } = 1;

    /// <summary>
    /// Is the given side charging this turn?
    /// </summary>
    public bool IsCharging(bool isAttackerSide) =>
        (Charge == ChargeState.AttackerCharged && isAttackerSide)
        || (Charge == ChargeState.DefenderCharged && !isAttackerSide);
}
