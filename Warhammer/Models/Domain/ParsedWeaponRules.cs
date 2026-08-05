using System.Text.RegularExpressions;

namespace Warhammer.Models.Domain;

public enum WeaponCondition { Always, WhenCharging, WhenBeingCharged, FirstRound }

/// <summary>
/// Parsed weapon special rules from the weapon's SpecialRules string field, plus the
/// modifiers and conditions the books state only in the profile's footnote prose.
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
    /// Additional Initiative bonus when charged to the front (wap-v2 Spear/Pike: +1 extra).
    /// </summary>
    public int ChargedToFrontInitiativeBonus { get; init; }

    /// <summary>
    /// When the Initiative modifier applies. Lances: WhenCharging. wap-v3 Spear/Pike/Polearm:
    /// FirstRound. Great weapon and wap-v2 Spear/Pike base mod: Always.
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

    /// <summary>
    /// Apply this weapon's Initiative modifier to a base Initiative value.
    /// Handles conditional mods (lance: when charging, spear: when being charged or in the
    /// first round) and the wap-v2 spear/pike charged-to-front bonus.
    /// </summary>
    public int ApplyToInitiative(int baseI, bool isCharging, bool isBeingCharged, int combatRound)
    {
        int effective = baseI;
        if (Holds(InitiativeModCondition, isCharging, isBeingCharged, combatRound))
            effective += InitiativeMod;
        if (isBeingCharged)
            effective += ChargedToFrontInitiativeBonus;
        return Math.Max(1, effective);
    }

    /// <summary>Does a condition hold for this attack?</summary>
    public static bool Holds(WeaponCondition condition, bool isCharging, bool isBeingCharged, int combatRound) =>
        condition switch
        {
            WeaponCondition.WhenCharging => isCharging,
            WeaponCondition.WhenBeingCharged => isBeingCharged,
            WeaponCondition.FirstRound => combatRound == 1,
            _ => true,
        };

    // The books wrap footnote prose at the column edge, so a phrase the parser looks for
    // arrives split by a newline ("in turns that the\nmodel charges"). Match on normalised
    // whitespace — a literal search silently fails and the modifier becomes unconditional,
    // which is how lances came to grant Mighty Blow on rounds nobody charged.
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // "gains +2 Initiative", "suffers -2 Initiative", "+1 to its Initiative".
    private static readonly Regex NotesInitiative = new(
        @"([+\-−]\d+)(?:\s+to\s+its)?\s+Initiative",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ParsedWeaponRules Parse(string? specialRules, string? notes = null)
    {
        string flat = notes == null ? "" : Whitespace.Replace(notes, " ");

        int initMod = 0;
        int extraAttacks = 0;
        int? mightyBlow = null;
        int? fier = null;
        int? parry = null;
        bool requiresTwoHands = false;
        bool initHasAsterisk = false;
        bool mbHasAsterisk = false;
        bool initFromRules = false;

        foreach (var part in (specialRules ?? "").Split(',').Select(p => p.Trim()))
        {
            var cleaned = part.TrimEnd('*');
            bool asterisk = part.TrimEnd().EndsWith('*');

            // Initiative modifier: "+1 Initiative" or "-2 Initiative"
            var initMatch = Regex.Match(cleaned, @"^([+-]\d+)\s+Initiative", RegexOptions.IgnoreCase);
            if (initMatch.Success)
            {
                initMod = int.Parse(initMatch.Groups[1].Value);
                initHasAsterisk = asterisk;
                initFromRules = true;
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
                requiresTwoHands = true;
        }

        // wap-v3 moved every weapon Initiative modifier out of the Special Rules column and
        // into the footnote ("A model with a great weapon suffers -2 Initiative"). Read it
        // from there when the rules column didn't carry one, and take its condition from the
        // same sentence — v3's spear is FirstRound for Initiative but WhenBeingCharged for
        // Mighty Blow, so one condition for the whole weapon would get one of them wrong.
        var initCondition = WeaponCondition.Always;
        if (initFromRules)
        {
            initCondition = initHasAsterisk ? ConditionIn(flat) : WeaponCondition.Always;
        }
        else
        {
            var fromNotes = NotesInitiative.Match(flat);
            if (fromNotes.Success)
            {
                initMod = int.Parse(fromNotes.Groups[1].Value.Replace('−', '-'));
                initCondition = ConditionIn(SentenceAround(flat, fromNotes.Index));
            }
        }

        int chargedBonus = 0;
        bool mbExcludesLight = false;
        if (flat.Length > 0)
        {
            // wap-v2 spear/pike: a second, separate +1 on top of the base mod when charged.
            if (flat.Contains("additional +1", StringComparison.OrdinalIgnoreCase)
                && flat.Contains("charged to its front", StringComparison.OrdinalIgnoreCase))
                chargedBonus = 1;

            mbExcludesLight = flat.Contains("Mighty Blow does not apply towards",
                StringComparison.OrdinalIgnoreCase);
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
            InitiativeModCondition = initCondition,
            MightyBlowCondition = mbHasAsterisk ? ConditionIn(flat) : WeaponCondition.Always,
            MightyBlowExcludesLightTargets = mbExcludesLight,
        };
    }

    /// <summary>Read a condition out of already-whitespace-normalised footnote prose.</summary>
    private static WeaponCondition ConditionIn(string flat)
    {
        if (flat.Contains("model charges", StringComparison.OrdinalIgnoreCase))
            return WeaponCondition.WhenCharging;
        if (flat.Contains("charged to its front", StringComparison.OrdinalIgnoreCase))
            return WeaponCondition.WhenBeingCharged;
        if (flat.Contains("first round of close combat", StringComparison.OrdinalIgnoreCase))
            return WeaponCondition.FirstRound;
        return WeaponCondition.Always;
    }

    /// <summary>The sentence containing <paramref name="index"/>, so a weapon whose footnote
    /// states two differently-conditioned modifiers doesn't cross-contaminate them.</summary>
    private static string SentenceAround(string flat, int index)
    {
        int start = flat.LastIndexOf('.', Math.Min(index, flat.Length - 1)) + 1;
        int end = flat.IndexOf('.', index);
        if (end < 0) end = flat.Length;
        return flat[start..end];
    }
}
