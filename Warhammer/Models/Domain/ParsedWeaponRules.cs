using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

public enum WeaponCondition { Always, WhenCharging, WhenBeingCharged }

/// <summary>
/// Parsed weapon special rules from the weapon's SpecialRules string field.
/// </summary>
public record ParsedWeaponRules
{
    public int InitiativeMod { get; init; }
    public int ExtraAttacks { get; init; }
    public int? MightyBlow { get; init; }
    public int? FightInExtraRanks { get; init; }
    public int? Parry { get; init; }
    public bool RequiresTwoHands { get; init; }

    /// <summary>
    /// Additional Initiative bonus when charged to the front (Spear/Pike: +1 extra).
    /// </summary>
    public int ChargedToFrontInitiativeBonus { get; init; }

    /// <summary>
    /// When the Initiative modifier applies.
    /// Lances: WhenCharging. Spear/Pike base mod: Always. Default: Always.
    /// </summary>
    public WeaponCondition InitiativeModCondition { get; init; }

    /// <summary>
    /// When the weapon's Mighty Blow applies.
    /// Spear/Pike: WhenBeingCharged. Lance: WhenCharging. Flail/Great Weapon: Always.
    /// </summary>
    public WeaponCondition MightyBlowCondition { get; init; }

    /// <summary>
    /// Spear/Pike: Mighty Blow does not apply vs Infantry, Large Infantry, Swarms, Shrines.
    /// </summary>
    public bool MightyBlowExcludesLightTargets { get; init; }

    public static ParsedWeaponRules Parse(string? specialRules, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(specialRules))
            return new ParsedWeaponRules();

        int initMod = 0;
        int extraAttacks = 0;
        int? mightyBlow = null;
        int? fier = null;
        int? parry = null;
        bool requiresTwoHands = false;
        bool initHasAsterisk = false;
        bool mbHasAsterisk = false;

        var parts = specialRules.Split(',').Select(p => p.Trim()).ToList();

        foreach (var part in parts)
        {
            var cleaned = part.TrimEnd('*');
            bool asterisk = part.TrimEnd().EndsWith('*');

            // Initiative modifier: "+1 Initiative" or "-2 Initiative"
            var initMatch = Regex.Match(cleaned, @"^([+-]\d+)\s+Initiative", RegexOptions.IgnoreCase);
            if (initMatch.Success)
            {
                initMod = int.Parse(initMatch.Groups[1].Value);
                initHasAsterisk = asterisk;
                continue;
            }

            // Extra attacks: "+1 Attack"
            var attackMatch = Regex.Match(cleaned, @"^\+(\d+)\s+Attack", RegexOptions.IgnoreCase);
            if (attackMatch.Success)
            {
                extraAttacks = int.Parse(attackMatch.Groups[1].Value);
                continue;
            }

            // Mighty Blow (N)
            var mbMatch = Regex.Match(cleaned, @"Mighty Blow\s*\((\d+)\)", RegexOptions.IgnoreCase);
            if (mbMatch.Success)
            {
                mightyBlow = int.Parse(mbMatch.Groups[1].Value);
                mbHasAsterisk = asterisk;
                continue;
            }

            // Fight in Extra Ranks (N)
            var fierMatch = Regex.Match(cleaned, @"Fight in Extra Ranks?\s*\((\d+)\)", RegexOptions.IgnoreCase);
            if (fierMatch.Success)
            {
                fier = int.Parse(fierMatch.Groups[1].Value);
                continue;
            }

            // Parry (N+)
            var parryMatch = Regex.Match(cleaned, @"Parry\s*\((\d)\+\)", RegexOptions.IgnoreCase);
            if (parryMatch.Success)
            {
                parry = int.Parse(parryMatch.Groups[1].Value);
                continue;
            }

            if (cleaned.Contains("Requires Two Hands", StringComparison.OrdinalIgnoreCase))
            {
                requiresTwoHands = true;
            }
        }

        // Determine condition type from notes
        // Lances: "the model charges" → WhenCharging
        // Spear/Pike: "charged to its front" → WhenBeingCharged
        var condition = WeaponCondition.Always;
        int chargedBonus = 0;
        bool mbExcludesLight = false;

        if (notes != null)
        {
            if (notes.Contains("the model charges", StringComparison.OrdinalIgnoreCase))
                condition = WeaponCondition.WhenCharging;
            else if (notes.Contains("charged to its front", StringComparison.OrdinalIgnoreCase))
                condition = WeaponCondition.WhenBeingCharged;

            // Spear/Pike: additional +1 Initiative when charged to front
            if (notes.Contains("additional +1", StringComparison.OrdinalIgnoreCase)
                && notes.Contains("charged to its front", StringComparison.OrdinalIgnoreCase))
                chargedBonus = 1;

            // Spear/Pike: MB doesn't apply vs Infantry, Large Infantry, Swarms, Shrines
            if (notes.Contains("Mighty Blow does not apply towards", StringComparison.OrdinalIgnoreCase))
                mbExcludesLight = true;
        }

        return new ParsedWeaponRules
        {
            InitiativeMod = initMod,
            ExtraAttacks = extraAttacks,
            MightyBlow = mightyBlow,
            FightInExtraRanks = fier,
            Parry = parry,
            RequiresTwoHands = requiresTwoHands,
            ChargedToFrontInitiativeBonus = chargedBonus,
            InitiativeModCondition = initHasAsterisk ? condition : WeaponCondition.Always,
            MightyBlowCondition = mbHasAsterisk ? condition : WeaponCondition.Always,
            MightyBlowExcludesLightTargets = mbExcludesLight
        };
    }
}
