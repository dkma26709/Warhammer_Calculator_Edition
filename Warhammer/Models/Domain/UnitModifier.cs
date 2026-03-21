namespace Warhammer.Models.Domain;

/// <summary>
/// A combat-relevant modifier applied to a unit from any source (magic items, spells, manual overrides).
/// All fields are nullable — null means "no effect on this stat."
/// Multiple modifiers compose: stat bonuses sum, booleans OR, saves take best.
/// </summary>
public record UnitModifier
{
    /// <summary>Display label for the modifier (e.g. item name or "Spell: Pha's Protection").</summary>
    public string Label { get; init; } = "";

    /// <summary>Source category: "Magic Item", "Spell", "Manual".</summary>
    public string Source { get; init; } = "";

    // ─── Stat bonuses (additive) ──────────────────────────────────

    /// <summary>Bonus to Weapon Skill (additive).</summary>
    public int? WeaponSkillBonus { get; init; }

    /// <summary>Bonus to Strength (additive, applied before weapon resolution).</summary>
    public int? StrengthBonus { get; init; }

    /// <summary>Bonus to Toughness (additive).</summary>
    public int? ToughnessBonus { get; init; }

    /// <summary>Bonus to Initiative (additive).</summary>
    public int? InitiativeBonus { get; init; }

    /// <summary>Bonus to Attacks per model (additive).</summary>
    public int? AttacksBonus { get; init; }

    /// <summary>Bonus to Wounds per model (additive).</summary>
    public int? WoundsBonus { get; init; }

    /// <summary>Bonus to armour save (positive = better save, e.g. +1 improves save by one step).</summary>
    public int? ArmourSaveBonus { get; init; }

    // ─── Roll modifiers (Tier 3 mechanics) ────────────────────────

    /// <summary>Modifier to the to-hit roll needed (negative = easier to hit, e.g. -1 means hit on one lower).</summary>
    public int? ToHitModifier { get; init; }

    /// <summary>Modifier to the to-wound roll needed (negative = easier to wound).</summary>
    public int? ToWoundModifier { get; init; }

    /// <summary>Fixed to-wound threshold overriding the S vs T table (e.g. 3 = "always wounds on 3+").</summary>
    public int? FixedToWound { get; init; }

    /// <summary>Fixed to-hit threshold overriding the WS table (e.g. 2 = "always hits on 2+").</summary>
    public int? FixedToHit { get; init; }

    /// <summary>Re-roll failed to-hit rolls.</summary>
    public bool RerollFailedToHit { get; init; }

    /// <summary>Re-roll failed to-wound rolls.</summary>
    public bool RerollFailedToWound { get; init; }

    // ─── Special rule grants ──────────────────────────────────────

    /// <summary>
    /// Partial <see cref="ParsedSpecialRules"/> overlay merged into the unit's base rules.
    /// Only set fields that the modifier grants — null fields are ignored during merge.
    /// </summary>
    public ParsedSpecialRules? RuleOverrides { get; init; }

    /// <summary>
    /// Aggregate multiple modifiers into a single combined modifier.
    /// Stat bonuses sum, booleans OR, rule overrides merge sequentially.
    /// </summary>
    public static UnitModifier Aggregate(IEnumerable<UnitModifier> modifiers)
    {
        int weaponSkillBonus = 0;
        int strengthBonus = 0;
        int toughnessBonus = 0;
        int initiativeBonus = 0;
        int attacksBonus = 0;
        int woundsBonus = 0;
        int armourSaveBonus = 0;
        int toHitModifier = 0;
        int toWoundModifier = 0;
        int? fixedToWound = null;
        int? fixedToHit = null;
        bool rerollFailedToHit = false;
        bool rerollFailedToWound = false;
        ParsedSpecialRules? mergedRules = null;

        foreach (var modifier in modifiers)
        {
            weaponSkillBonus += modifier.WeaponSkillBonus ?? 0;
            strengthBonus += modifier.StrengthBonus ?? 0;
            toughnessBonus += modifier.ToughnessBonus ?? 0;
            initiativeBonus += modifier.InitiativeBonus ?? 0;
            attacksBonus += modifier.AttacksBonus ?? 0;
            woundsBonus += modifier.WoundsBonus ?? 0;
            armourSaveBonus += modifier.ArmourSaveBonus ?? 0;
            toHitModifier += modifier.ToHitModifier ?? 0;
            toWoundModifier += modifier.ToWoundModifier ?? 0;

            if (modifier.FixedToWound.HasValue)
                fixedToWound = fixedToWound.HasValue
                    ? Math.Min(fixedToWound.Value, modifier.FixedToWound.Value)
                    : modifier.FixedToWound.Value;

            if (modifier.FixedToHit.HasValue)
                fixedToHit = fixedToHit.HasValue
                    ? Math.Min(fixedToHit.Value, modifier.FixedToHit.Value)
                    : modifier.FixedToHit.Value;

            rerollFailedToHit |= modifier.RerollFailedToHit;
            rerollFailedToWound |= modifier.RerollFailedToWound;

            if (modifier.RuleOverrides != null)
                mergedRules = mergedRules?.Merge(modifier.RuleOverrides) ?? modifier.RuleOverrides;
        }

        return new UnitModifier
        {
            Label = "Aggregate",
            Source = "Combined",
            WeaponSkillBonus = weaponSkillBonus != 0 ? weaponSkillBonus : null,
            StrengthBonus = strengthBonus != 0 ? strengthBonus : null,
            ToughnessBonus = toughnessBonus != 0 ? toughnessBonus : null,
            InitiativeBonus = initiativeBonus != 0 ? initiativeBonus : null,
            AttacksBonus = attacksBonus != 0 ? attacksBonus : null,
            WoundsBonus = woundsBonus != 0 ? woundsBonus : null,
            ArmourSaveBonus = armourSaveBonus != 0 ? armourSaveBonus : null,
            ToHitModifier = toHitModifier != 0 ? toHitModifier : null,
            ToWoundModifier = toWoundModifier != 0 ? toWoundModifier : null,
            FixedToWound = fixedToWound,
            FixedToHit = fixedToHit,
            RerollFailedToHit = rerollFailedToHit,
            RerollFailedToWound = rerollFailedToWound,
            RuleOverrides = mergedRules,
        };
    }
}
