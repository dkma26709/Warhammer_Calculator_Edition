namespace Warhammer.Calculator.Models;

public enum ChargeState { Neither, AttackerCharged, DefenderCharged }

public record CombatContext
{
    public ChargeState Charge { get; init; } = ChargeState.Neither;
    public int CombatRound { get; init; } = 1;
}
