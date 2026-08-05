namespace Warhammer.Calculator.Models;

/// <summary>
/// Who charged into this combat. <see cref="BothCharged"/> is a counter-charge: one of wap-v3's
/// four charge reactions, alongside Hold, Stand &amp; Shoot and Flee. The charged unit rushes
/// forward to meet the attacker head-on and "both units are considered to have charged during
/// this turn" (3.11 p17). wap-v2's core rules have no counter-charge; there it exists only as
/// the Empire State Troops action, which instead strikes an unengaged arc.
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
    /// charge reaction". Counter-charge is a different reaction, so a counter-charging unit
    /// does not qualify for them even though it was itself charged.
    /// </summary>
    public bool IsHoldingAgainstCharge(bool isAttackerSide) =>
        IsCharging(!isAttackerSide) && !IsCharging(isAttackerSide);

    /// <summary>
    /// Get the faction key for the opponent of the given side.
    /// </summary>
    public string? GetOpponentFactionKey(bool isAttackerSide) =>
        isAttackerSide ? DefenderFactionKey : AttackerFactionKey;
}
