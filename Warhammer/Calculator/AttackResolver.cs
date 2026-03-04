using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class AttackResolver
{
    private readonly CoreRulesService _coreRules;

    public AttackResolver(CoreRulesService coreRules)
    {
        _coreRules = coreRules;
    }

    /// <summary>
    /// Lookup function that resolves armour key to its CombatSave field from core-rules.json.
    /// </summary>
    public Func<string, string?> GetArmourSaveLookup()
    {
        return key =>
        {
            var entry = _coreRules.GetArmour(key);
            return entry?.CombatSave;
        };
    }

    /// <summary>
    /// Resolve all attacks from one unit against another.
    /// </summary>
    public AttackResult ResolveAttacks(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        WeaponEntry weapon,
        CombatContext context,
        bool isMountAttack,
        Random rng)
    {
        var attackerRules = attacker.SpecialRules;
        var defenderRules = defender.SpecialRules;
        bool isCharging = IsCharging(attacker, defender, context);
        bool isFirstRound = context.CombatRound == 1;

        // Parse weapon rules
        var weaponRules = isMountAttack ? null : ParsedWeaponRules.Parse(weapon.SpecialRules, weapon.Notes);

        // Determine profile to use
        var profile = isMountAttack ? attacker.MountProfile! : attacker.PrimaryProfile;

        // Count attacks
        int totalAttacks;
        if (isMountAttack)
        {
            totalAttacks = attacker.MountAttacks();
        }
        else
        {
            int weaponExtraAttacks = weaponRules?.ExtraAttacks ?? 0;
            int weaponFiER = weaponRules?.FightInExtraRanks ?? 0;
            totalAttacks = attacker.TotalAttacks(isCharging, context.CombatRound,
                weaponExtraAttacks, weaponFiER);
        }

        if (totalAttacks <= 0) return new AttackResult();

        // Effective strength (includes Mighty Blow from unit and/or weapon, using highest)
        int baseS = profile.S ?? 3;
        int effectiveStrength;
        if (isMountAttack)
        {
            effectiveStrength = baseS; // Mounts use their own strength, no weapon
        }
        else
        {
            // Determine weapon's applicable Mighty Blow
            int weaponMB = 0;
            if (weaponRules?.MightyBlow != null)
            {
                bool applyWeaponMB = true;
                // Conditional: Spear MB only when charged to front
                if (weaponRules.MightyBlowOnlyWhenCharged)
                {
                    // Unit is being charged if opponent charged this unit
                    bool unitIsBeingCharged =
                        (context.Charge == ChargeState.AttackerCharged && !isCharging) ||
                        (context.Charge == ChargeState.DefenderCharged && isCharging);
                    applyWeaponMB = unitIsBeingCharged;
                }
                if (applyWeaponMB)
                    weaponMB = weaponRules.MightyBlow.Value;
            }
            effectiveStrength = attacker.EffectiveStrength(weapon, isCharging, context.CombatRound, weaponMB);
        }

        // To-Hit
        int attackerWS = profile.WS ?? 1;
        int defenderWS = (defender.MountProfile != null && defender.TroopType.HasSplitProfile)
            ? Math.Max(defender.PrimaryProfile.WS ?? 1, defender.MountProfile.WS ?? 1)
            : defender.PrimaryProfile.WS ?? 1;
        int toHitNeeded = _coreRules.GetToHitScore(attackerWS, defenderWS);

        // Re-roll flags
        bool hasHatredReroll = attackerRules.Hatred && isFirstRound;
        bool hasASFReroll = attackerRules.AlwaysStrikesFirst
            && !attackerRules.AlwaysStrikesLast
            && (profile.I ?? 1) > (defender.PrimaryProfile.I ?? 1);
        bool hasASLPenalty = !attackerRules.AlwaysStrikesFirst
            && attackerRules.AlwaysStrikesLast
            && (profile.I ?? 1) < (defender.PrimaryProfile.I ?? 1);

        // Roll To Hit
        int hits = 0;
        for (int i = 0; i < totalAttacks; i++)
        {
            int roll = rng.Next(1, 7);
            bool hit = roll == 6 || (roll != 1 && roll >= toHitNeeded);

            if (!hit && (hasHatredReroll || hasASFReroll))
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= toHitNeeded);
            }
            else if (hit && hasASLPenalty)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= toHitNeeded);
            }

            if (hit) hits++;
        }

        if (hits == 0) return new AttackResult { TotalHits = 0 };

        // Effective toughness
        int defenderT = (defender.MountProfile != null && defender.TroopType.HasSplitProfile)
            ? Math.Max(defender.PrimaryProfile.T ?? 3, defender.MountProfile.T ?? 3)
            : defender.PrimaryProfile.T ?? 3;

        // To-Wound
        int toWoundNeeded = _coreRules.GetToWoundScore(effectiveStrength, defenderT);

        // Poison modifier: +1 to wound (lower the needed score by 1)
        int poisonMod = (!isMountAttack && attackerRules.PoisonedAttacks) ? 1 : 0;
        int effectiveToWound = Math.Max(2, toWoundNeeded - poisonMod);

        // Flaming re-roll vs Flammable
        bool flamingReroll = !isMountAttack && attackerRules.FlamingAttacks && defenderRules.Flammable;

        int totalWoundsRolled = 0;
        int killingBlows = 0;
        int normalWounds = 0;
        bool[] woundIgnoresArmour = new bool[hits]; // track per wound
        int woundIdx = 0;

        for (int i = 0; i < hits; i++)
        {
            int roll = rng.Next(1, 7);
            bool wounded = roll == 6 || (roll != 1 && roll >= effectiveToWound);

            if (!wounded && flamingReroll)
            {
                roll = rng.Next(1, 7);
                wounded = roll == 6 || (roll != 1 && roll >= effectiveToWound);
            }

            if (!wounded) continue;

            totalWoundsRolled++;

            // Killing Blow check (only on natural 6 to wound, or 5+ if dual source)
            bool isKB = !isMountAttack && attackerRules.KillingBlow && roll == 6;
            bool isHKB = !isMountAttack && attackerRules.HeroicKillingBlow && roll == 6;

            if (isKB || isHKB)
            {
                bool eligible = IsKBEligible(defender.TroopType, isHKB);
                if (eligible)
                {
                    killingBlows++;
                }
                else
                {
                    // Non-eligible target: 1 wound ignoring armour and regen
                    if (woundIdx < woundIgnoresArmour.Length)
                        woundIgnoresArmour[woundIdx] = true;
                    normalWounds++;
                    woundIdx++;
                }
            }
            else
            {
                normalWounds++;
                woundIdx++;
            }
        }

        // Armour Saves for normal wounds
        int ap = (!isMountAttack ? attackerRules.ArmourPiercing : null) ?? 0;
        int strengthMod = _coreRules.GetArmourSaveModifier(effectiveStrength);
        bool ignoresArmour = !isMountAttack && (attackerRules.IgnoresArmourSaves);
        bool lightningAttacks = !isMountAttack && attackerRules.LightningAttacks;

        int unsavedNormal = 0;
        for (int i = 0; i < normalWounds; i++)
        {
            bool thisIgnoresArmour = ignoresArmour || (i < woundIgnoresArmour.Length && woundIgnoresArmour[i]);

            if (thisIgnoresArmour || lightningAttacks)
            {
                if (lightningAttacks && defenderRules.NaturalArmour.HasValue)
                {
                    // Lightning: still saved by Natural Armour
                    int natSave = defenderRules.NaturalArmour.Value;
                    int modNatSave = natSave - strengthMod; // strength still modifies
                    if (modNatSave >= 2 && rng.Next(1, 7) >= modNatSave)
                        continue; // saved by natural armour
                }
                unsavedNormal++;
            }
            else
            {
                int armourSave = defender.EffectiveArmourSave(effectiveStrength, ap, strengthMod, GetArmourSaveLookup());
                if (armourSave <= 6 && rng.Next(1, 7) >= armourSave)
                    continue; // saved
                unsavedNormal++;
            }
        }

        // Ward Saves / Regeneration
        int wardValue = defenderRules.WardSave ?? 7;
        int regenValue = defenderRules.Regeneration ?? 7;
        int parryValue = defenderRules.Parry ?? 7;

        // Ethereal override
        if (defenderRules.Ethereal)
        {
            bool magical = !isMountAttack && attackerRules.MagicalAttacks;
            int etherealWard = magical ? 5 : 2;
            wardValue = Math.Min(wardValue, etherealWard);
        }

        // Parry restrictions: doesn't work vs S >= defender S+3
        int defenderS = defender.PrimaryProfile.S ?? 3;
        if (effectiveStrength >= defenderS + 3)
            parryValue = 7;

        // Flaming negates regeneration
        bool flamingNegatesRegen = !isMountAttack && attackerRules.FlamingAttacks;

        // KB wounds: only Ward Save applies (no regen, no parry)
        int kbUnsaved = 0;
        for (int i = 0; i < killingBlows; i++)
        {
            if (wardValue <= 6 && rng.Next(1, 7) >= wardValue)
                continue; // ward saved
            kbUnsaved++;
        }

        // Normal wounds: best of ward/regen/parry
        int effectiveWard;
        if (flamingNegatesRegen)
            effectiveWard = Math.Min(wardValue, parryValue);
        else
            effectiveWard = Math.Min(Math.Min(wardValue, regenValue), parryValue);

        int normalUnsaved = 0;
        for (int i = 0; i < unsavedNormal; i++)
        {
            if (effectiveWard <= 6 && rng.Next(1, 7) >= effectiveWard)
                continue; // saved
            normalUnsaved++;
        }

        // Multiple Wounds
        int multipleWoundsValue = (!isMountAttack ? attackerRules.MultipleWounds : null) ?? 1;
        string? multipleWoundsRoll = !isMountAttack ? attackerRules.MultipleWoundsRoll : null;
        int defenderW = defender.PrimaryProfile.W ?? 1;

        // Track wounds model-by-model: Multiple Wounds and KB don't carry over between models
        int modelsRemaining = defender.ModelCount;
        int currentModelHP = defenderW; // wounds remaining on the frontmost model
        int finalWounds = 0;

        // KB wounds: each kills one model outright (full W inflicted, remainder lost)
        for (int i = 0; i < kbUnsaved; i++)
        {
            if (modelsRemaining <= 0) break;
            finalWounds += currentModelHP; // kill current model (inflict its remaining HP)
            modelsRemaining--;
            currentModelHP = defenderW; // next model starts at full HP
        }

        // Normal unsaved wounds (with Multiple Wounds if applicable)
        bool hasMultipleWounds = multipleWoundsValue > 1 || multipleWoundsRoll != null;
        for (int i = 0; i < normalUnsaved; i++)
        {
            if (modelsRemaining <= 0) break;

            int damagePerHit = hasMultipleWounds
                ? RollMultipleWounds(multipleWoundsValue, multipleWoundsRoll, rng)
                : 1;

            // MW capped at current model's remaining HP — excess is lost, not carried over
            int actualDamage = Math.Min(damagePerHit, currentModelHP);
            finalWounds += actualDamage;
            currentModelHP -= actualDamage;

            if (currentModelHP <= 0)
            {
                modelsRemaining--;
                currentModelHP = defenderW;
            }
        }

        return new AttackResult
        {
            TotalHits = hits,
            TotalWoundsInflicted = totalWoundsRolled,
            KillingBlowWounds = kbUnsaved,
            UnsavedWounds = finalWounds
        };
    }

    /// <summary>
    /// Resolve impact hits (charge only). Mighty Blow applies to impact hits in first round.
    /// </summary>
    public int ResolveImpactHits(
        UnitConfiguration charger,
        UnitConfiguration target,
        CombatContext context,
        Random rng)
    {
        int impactHits = GetImpactHitCount(charger, rng);
        if (impactHits <= 0) return 0;

        // Impact hits use mount's S for cavalry troop type, otherwise model's S
        int strength = charger.MountProfile?.S ?? charger.PrimaryProfile.S ?? 3;

        // Mighty Blow applies to impact hits in first round
        if (context.CombatRound == 1 && charger.SpecialRules.MightyBlow.HasValue)
            strength += charger.SpecialRules.MightyBlow.Value;

        int defenderT = (target.MountProfile != null && target.TroopType.HasSplitProfile)
            ? Math.Max(target.PrimaryProfile.T ?? 3, target.MountProfile.T ?? 3)
            : target.PrimaryProfile.T ?? 3;

        int toWoundNeeded = _coreRules.GetToWoundScore(strength, defenderT);
        int strengthMod = _coreRules.GetArmourSaveModifier(strength);

        int wounds = 0;
        for (int i = 0; i < impactHits; i++)
        {
            if (rng.Next(1, 7) < toWoundNeeded) continue; // failed to wound

            // Armour save (no AP from impact hits)
            int armourSave = target.EffectiveArmourSave(strength, 0, strengthMod, GetArmourSaveLookup());
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue; // saved

            // Ward save (no parry on impact hits)
            int wardValue = target.SpecialRules.WardSave ?? 7;
            int regenValue = target.SpecialRules.Regeneration ?? 7;
            int effectiveWard = Math.Min(wardValue, regenValue);

            if (target.SpecialRules.Ethereal)
                effectiveWard = Math.Min(effectiveWard, 2); // mundane impact hits vs ethereal

            if (effectiveWard <= 6 && rng.Next(1, 7) >= effectiveWard) continue; // ward saved

            wounds++;
        }

        return wounds;
    }

    private static int GetImpactHitCount(UnitConfiguration unit, Random rng)
    {
        // From special rules
        int fromRules = 0;
        if (unit.SpecialRules.ImpactHits.HasValue)
            fromRules = unit.SpecialRules.ImpactHits.Value * unit.ModelsInFrontRank;
        else if (unit.SpecialRules.ImpactHitsRoll != null)
            fromRules = RollDice(unit.SpecialRules.ImpactHitsRoll, rng) * unit.ModelsInFrontRank;

        // From troop type (Cavalry: 1 per model, Monstrous Cavalry: 1 per model)
        int fromType = unit.TroopType.BaseType switch
        {
            BaseTroopType.Cavalry => unit.ModelsInFrontRank,
            BaseTroopType.MonstrousCavalry => unit.ModelsInFrontRank,
            _ => 0
        };

        // Use highest single source (don't stack)
        return Math.Max(fromRules, fromType);
    }

    private static bool IsKBEligible(TroopTypeInfo targetType, bool isHeroic)
    {
        if (isHeroic) return true; // HKB works against everything

        // KB works against Infantry, Large Infantry, Cavalry, War Beasts
        return targetType.BaseType is
            BaseTroopType.Infantry or
            BaseTroopType.LargeInfantry or
            BaseTroopType.Cavalry or
            BaseTroopType.WarBeasts;
    }

    private static bool IsCharging(UnitConfiguration attacker, UnitConfiguration defender, CombatContext context)
    {
        return context.Charge == ChargeState.AttackerCharged;
    }

    private static int RollMultipleWounds(int fixedValue, string? rollExpr, Random rng)
    {
        if (rollExpr != null)
            return RollDice(rollExpr, rng);
        return fixedValue;
    }

    private static int RollDice(string expr, Random rng)
    {
        // Handle "D6", "D3", "D3+1", "2D6", etc.
        var upper = expr.ToUpperInvariant().Trim();

        if (upper == "D6") return rng.Next(1, 7);
        if (upper == "D3") return rng.Next(1, 4);

        // D3+N or D6+N
        if (upper.StartsWith("D3+") && int.TryParse(upper[3..], out var d3mod))
            return rng.Next(1, 4) + d3mod;
        if (upper.StartsWith("D6+") && int.TryParse(upper[3..], out var d6mod))
            return rng.Next(1, 7) + d6mod;

        // 2D6
        if (upper == "2D6")
            return rng.Next(1, 7) + rng.Next(1, 7);

        return 1;
    }
}
