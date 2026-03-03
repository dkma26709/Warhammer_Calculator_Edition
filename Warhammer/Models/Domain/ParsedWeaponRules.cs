using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

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
    /// Additional Initiative bonus when charged to the front (Spear: +1 extra on top of base +1).
    /// </summary>
    public int ChargedToFrontInitiativeBonus { get; init; }

    /// <summary>
    /// Mighty Blow only applies when charged to front (Spear's conditional MB).
    /// </summary>
    public bool MightyBlowOnlyWhenCharged { get; init; }

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
        int chargedBonus = 0;
        bool mbOnlyWhenCharged = false;

        var parts = specialRules.Split(',').Select(p => p.Trim()).ToList();

        foreach (var part in parts)
        {
            var cleaned = part.TrimEnd('*');

            // Initiative modifier: "+1 Initiative" or "-2 Initiative"
            var initMatch = Regex.Match(cleaned, @"^([+-]\d+)\s+Initiative", RegexOptions.IgnoreCase);
            if (initMatch.Success)
            {
                initMod = int.Parse(initMatch.Groups[1].Value);
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
                // Check if this part had an asterisk (conditional)
                if (part.TrimEnd().EndsWith('*'))
                    mbOnlyWhenCharged = true;
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

        // Check notes for charged-to-front Initiative bonus (Spear)
        if (notes != null && notes.Contains("charged to its front", StringComparison.OrdinalIgnoreCase)
            && notes.Contains("additional +1", StringComparison.OrdinalIgnoreCase))
        {
            chargedBonus = 1;
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
            MightyBlowOnlyWhenCharged = mbOnlyWhenCharged
        };
    }
}
