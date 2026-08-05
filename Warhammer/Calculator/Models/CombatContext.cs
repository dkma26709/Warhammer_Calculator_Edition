namespace Warhammer.Calculator.Models;

/// <summary>
/// Who charged into this combat. <see cref="BothCharged"/> covers a counter-charge — the
/// wap-v2 Empire State Troops action resolves "as if it had just declared a charge", so both
/// units count as charging.
/// </summary>
public enum ChargeState { Neither, AttackerCharged, DefenderCharged, BothCharged }

public record CombatContext
{
    public ChargeState Charge { get; init; } = ChargeState.Neither;
    public int CombatRound { get; init; } = 1;
    public string? AttackerFactionKey { get; init; }
    public string? DefenderFactionKey { get; init; }

    /// <summary>
    /// Is the given side charging this turn? True for both sides on a counter-charge.
    /// </summary>
    public bool IsCharging(bool isAttackerSide) => Charge switch
    {
        ChargeState.AttackerCharged => isAttackerSide,
        ChargeState.DefenderCharged => !isAttackerSide,
        ChargeState.BothCharged => true,
        _ => false,
    };

    /// <summary>
    /// Did this side receive a charge while standing still? Spear and pike bonuses need the
    /// model to have held — wap-v3 spells it out: "chose either Hold or Stand &amp; Shoot as a
    /// charge reaction" — so a unit that counter-charged does not qualify for them even though
    /// it was itself charged.
    /// </summary>
    public bool IsHoldingAgainstCharge(bool isAttackerSide) =>
        IsCharging(!isAttackerSide) && !IsCharging(isAttackerSide);

    /// <summary>
    /// Get the faction key for the opponent of the given side.
    /// </summary>
    public string? GetOpponentFactionKey(bool isAttackerSide) =>
        isAttackerSide ? DefenderFactionKey : AttackerFactionKey;
}
