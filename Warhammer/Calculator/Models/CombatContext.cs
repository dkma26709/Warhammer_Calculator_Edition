namespace Warhammer.Calculator.Models;

public enum ChargeState { Neither, AttackerCharged, DefenderCharged }

public record CombatContext
{
    public ChargeState Charge { get; init; } = ChargeState.Neither;
    public int CombatRound { get; init; } = 1;
    public string? AttackerFactionKey { get; init; }
    public string? DefenderFactionKey { get; init; }

    /// <summary>
    /// Is the given side charging this turn?
    /// </summary>
    public bool IsCharging(bool isAttackerSide) =>
        (Charge == ChargeState.AttackerCharged && isAttackerSide)
        || (Charge == ChargeState.DefenderCharged && !isAttackerSide);

    /// <summary>
    /// Get the faction key for the opponent of the given side.
    /// </summary>
    public string? GetOpponentFactionKey(bool isAttackerSide) =>
        isAttackerSide ? DefenderFactionKey : AttackerFactionKey;
}
