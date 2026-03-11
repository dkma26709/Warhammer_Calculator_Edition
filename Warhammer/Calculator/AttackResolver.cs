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
    /// Resolve all attacks from one unit against another.
    /// Accepts pre-cached weapon rules and defender parry to avoid per-iteration allocations.
    /// </summary>
    public AttackResult ResolveAttacks(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        WeaponEntry weapon,
        CombatContext context,
        bool isMountAttack,
        bool isAttackerSide,
        ParsedWeaponRules? cachedWeaponRules,
        int cachedDefenderParry,
        Random rng,
        Func<string, string?> armourSaveLookup,
        int cachedDefenderBaseArmour = -1,
        int cachedEffectiveDefenderI = -1)
    {
        var attackerRules = attacker.SpecialRules;
        var defenderRules = defender.SpecialRules;
        bool isRider = !isMountAttack;
        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsCharging(!isAttackerSide);
        bool isFirstRound = context.CombatRound == 1;

        var weaponRules = isRider ? cachedWeaponRules : null;
        var profile = isMountAttack ? attacker.MountProfile! : attacker.PrimaryProfile;

        // ── Count attacks ──────────────────────────────────────────

        int totalAttacks = CountAttacks(attacker, attackerRules, weaponRules,
            isCharging, context.CombatRound, isMountAttack, rng);
        if (totalAttacks <= 0) return default;

        // ── Effective strength ─────────────────────────────────────

        int effectiveStrength = ComputeEffectiveStrength(
            attacker, defender, weapon, weaponRules, profile,
            isMountAttack, isCharging, isBeingCharged, context.CombatRound);

        // ── Roll to hit ────────────────────────────────────────────

        int toHitNeeded = _coreRules.GetToHitScore(profile.WS ?? 1, defender.BestWS);

        int effectiveAttackerI = profile.I ?? 1;
        if (isRider && weaponRules != null)
            effectiveAttackerI = weaponRules.ApplyToInitiative(effectiveAttackerI, isCharging, isBeingCharged);
        int effectiveDefenderI = cachedEffectiveDefenderI >= 0
            ? cachedEffectiveDefenderI
            : ComputeEffectiveI(defender, context, !isAttackerSide);

        bool rerollMisses = (attackerRules.Hatred && isFirstRound)
            || (attackerRules.AlwaysStrikesFirst && !attackerRules.AlwaysStrikesLast
                && effectiveAttackerI > effectiveDefenderI);
        bool rerollHits = !attackerRules.AlwaysStrikesFirst
            && attackerRules.AlwaysStrikesLast
            && effectiveAttackerI < effectiveDefenderI;

        int hits = RollToHit(totalAttacks, toHitNeeded, rerollMisses, rerollHits, rng);
        if (hits == 0) return new AttackResult { TotalHits = 0 };

        // ── Roll to wound ──────────────────────────────────────────

        int toWoundNeeded = _coreRules.GetToWoundScore(effectiveStrength, defender.BestT);
        int poisonMod = (isRider && attackerRules.PoisonedAttacks) ? 1 : 0;
        int effectiveToWound = Math.Max(2, toWoundNeeded - poisonMod);
        bool flamingReroll = isRider && attackerRules.FlamingAttacks && defenderRules.Flammable;

        var (totalWoundsRolled, killingBlows, nonEligibleKBWounds, normalWounds) =
            RollToWound(hits, effectiveToWound, flamingReroll,
                isRider && attackerRules.KillingBlow,
                isRider && attackerRules.HeroicKillingBlow,
                defender.TroopType, rng);

        // ── Armour saves (normal wounds only; non-eligible KB skip armour) ──

        int ap = (isRider ? attackerRules.ArmourPiercing : null) ?? 0;
        int strengthMod = _coreRules.GetArmourSaveModifier(effectiveStrength);
        bool ignoresArmour = isRider && attackerRules.IgnoresArmourSaves;
        bool lightningAttacks = isRider && attackerRules.LightningAttacks;

        int defBaseArmour = cachedDefenderBaseArmour >= 0
            ? cachedDefenderBaseArmour
            : defender.ComputeBaseArmourModifier(armourSaveLookup);
        int unsavedNormal = RollArmourSaves(normalWounds, defender, defenderRules,
            ap, strengthMod, ignoresArmour, lightningAttacks, rng, defBaseArmour);

        // ── Ward saves ─────────────────────────────────────────────

        int wardValue = defenderRules.WardSave ?? 7;
        int regenValue = defenderRules.Regeneration ?? 7;
        int parryValue = cachedDefenderParry;
        bool flamingNegatesRegen = isRider && attackerRules.FlamingAttacks;

        if (defenderRules.Ethereal)
        {
            int etherealWard = (isRider && attackerRules.MagicalAttacks) ? 7 : 2;
            wardValue = Math.Min(wardValue, etherealWard);
        }

        int kbUnsaved = RollWardSaves(killingBlows, wardValue, rng);
        int nonEligKBUnsaved = RollWardSaves(nonEligibleKBWounds, Math.Min(wardValue, parryValue), rng);

        int effectiveWard = flamingNegatesRegen
            ? Math.Min(wardValue, parryValue)
            : Math.Min(Math.Min(wardValue, regenValue), parryValue);
        int normalUnsaved = RollWardSaves(unsavedNormal, effectiveWard, rng);

        // ── Wound allocation ───────────────────────────────────────

        int mwValue = (isRider ? attackerRules.MultipleWounds : null) ?? 1;
        string? mwRoll = isRider ? attackerRules.MultipleWoundsRoll : null;

        int finalWounds = AllocateWoundsToModels(
            kbUnsaved, nonEligKBUnsaved, normalUnsaved,
            defender.PrimaryProfile.W ?? 1, defender.ModelCount, mwValue, mwRoll, rng);

        return new AttackResult
        {
            TotalHits = hits,
            TotalWoundsInflicted = totalWoundsRolled,
            KillingBlowWounds = kbUnsaved,
            UnsavedWounds = finalWounds
        };
    }

    /// <summary>
    /// Resolve impact hits (charge only).
    /// </summary>
    public int ResolveImpactHits(
        UnitConfiguration charger,
        UnitConfiguration target,
        CombatContext context,
        Random rng,
        Func<string, string?> armourSaveLookup)
    {
        int impactHits = GetImpactHitCount(charger, rng);
        if (impactHits <= 0) return 0;

        int strength = charger.MountProfile?.S ?? charger.PrimaryProfile.S ?? 3;
        if (context.CombatRound == 1 && charger.SpecialRules.MightyBlow.HasValue)
            strength += charger.SpecialRules.MightyBlow.Value;

        int toWoundNeeded = _coreRules.GetToWoundScore(strength, target.BestT);
        int strengthMod = _coreRules.GetArmourSaveModifier(strength);

        bool flamingImpact = charger.SpecialRules.FlamingAttacks;
        int baseWard = target.SpecialRules.WardSave ?? 7;
        int baseRegen = target.SpecialRules.Regeneration ?? 7;
        int impactWard = flamingImpact ? baseWard : Math.Min(baseWard, baseRegen);

        if (target.SpecialRules.Ethereal)
        {
            int etherealWard = charger.SpecialRules.MagicalAttacks ? 7 : 2;
            impactWard = Math.Min(impactWard, etherealWard);
        }

        int wounds = 0;
        for (int i = 0; i < impactHits; i++)
        {
            if (rng.Next(1, 7) < toWoundNeeded) continue;

            int armourSave = target.EffectiveArmourSave(strength, 0, strengthMod, armourSaveLookup);
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue;

            if (impactWard <= 6 && rng.Next(1, 7) >= impactWard) continue;

            wounds++;
        }

        return wounds;
    }

    /// <summary>
    /// Compute defender parry value for a given attacker weapon/rules combination.
    /// Called once during group preparation.
    /// </summary>
    public int ComputeDefenderParry(
        UnitConfiguration defender,
        UnitConfiguration attacker,
        WeaponEntry attackerWeapon,
        ParsedWeaponRules? attackerWeaponRules,
        CombatContext context,
        bool isAttackerSide)
    {
        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsCharging(!isAttackerSide);
        var profile = attacker.PrimaryProfile;
        int effectiveStrength = ComputeEffectiveStrength(
            attacker, defender, attackerWeapon, attackerWeaponRules, profile,
            false, isCharging, isBeingCharged, context.CombatRound);

        return ResolveDefenderParry(defender, effectiveStrength, false);
    }

    // ─── Private helpers ───────────────────────────────────────────

    private int ComputeEffectiveI(UnitConfiguration unit, CombatContext context, bool isAttackerSide)
    {
        int baseI = unit.PrimaryProfile.I ?? 1;
        var weapon = _coreRules.GetCloseCombatWeapon(unit.SelectedWeaponKey);
        if (weapon == null) return baseI;

        var rules = ParsedWeaponRules.Parse(weapon.SpecialRules);
        return rules.ApplyToInitiative(baseI,
            context.IsCharging(isAttackerSide),
            context.IsCharging(!isAttackerSide));
    }

    private static int CountAttacks(
        UnitConfiguration attacker, ParsedSpecialRules rules,
        ParsedWeaponRules? weaponRules, bool isCharging, int combatRound,
        bool isMountAttack, Random rng)
    {
        if (isMountAttack) return attacker.MountAttacks();

        int? attacksOverride = null;
        if (rules.RandomAttacksRoll != null)
            attacksOverride = DiceRoller.Roll(rules.RandomAttacksRoll, rng);
        else if (rules.RandomAttacks.HasValue)
            attacksOverride = rules.RandomAttacks.Value;

        return attacker.TotalAttacks(isCharging, combatRound,
            weaponRules?.ExtraAttacks ?? 0,
            weaponRules?.FightInExtraRanks ?? 0,
            attacksOverride);
    }

    private static int ComputeEffectiveStrength(
        UnitConfiguration attacker, UnitConfiguration defender,
        WeaponEntry weapon, ParsedWeaponRules? weaponRules,
        ProfileEntry profile, bool isMountAttack,
        bool isCharging, bool isBeingCharged, int combatRound)
    {
        if (isMountAttack) return profile.S ?? 3;

        int weaponMB = 0;
        if (weaponRules?.MightyBlow != null)
        {
            bool applyWeaponMB = weaponRules.MightyBlowCondition switch
            {
                WeaponCondition.WhenCharging => isCharging,
                WeaponCondition.WhenBeingCharged => isBeingCharged,
                _ => true
            };

            if (applyWeaponMB && weaponRules.MightyBlowExcludesLightTargets)
            {
                bool targetIsLight = defender.TroopType.BaseType is
                    BaseTroopType.Infantry or BaseTroopType.LargeInfantry or
                    BaseTroopType.Swarms or BaseTroopType.Shrine;
                if (targetIsLight)
                    applyWeaponMB = false;
            }

            if (applyWeaponMB)
                weaponMB = weaponRules.MightyBlow.Value;
        }

        return attacker.EffectiveStrength(weapon, isCharging, combatRound, weaponMB);
    }

    private static int RollToHit(int attacks, int needed, bool rerollMisses, bool rerollHits, Random rng)
    {
        int hits = 0;
        for (int i = 0; i < attacks; i++)
        {
            int roll = rng.Next(1, 7);
            bool hit = roll == 6 || (roll != 1 && roll >= needed);

            if (!hit && rerollMisses)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
            }
            else if (hit && rerollHits)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
            }

            if (hit) hits++;
        }
        return hits;
    }

    private static (int TotalWounds, int KillingBlows, int NonEligibleKB, int NormalWounds) RollToWound(
        int hits, int toWoundNeeded, bool flamingReroll,
        bool hasKB, bool hasHKB, TroopTypeInfo defenderType, Random rng)
    {
        int totalWounds = 0, killingBlows = 0, nonEligKB = 0, normalWounds = 0;

        for (int i = 0; i < hits; i++)
        {
            int roll = rng.Next(1, 7);
            bool wounded = roll == 6 || (roll != 1 && roll >= toWoundNeeded);

            if (!wounded && flamingReroll)
            {
                roll = rng.Next(1, 7);
                wounded = roll == 6 || (roll != 1 && roll >= toWoundNeeded);
            }

            if (!wounded) continue;
            totalWounds++;

            bool isKB = hasKB && roll == 6;
            bool isHKB = hasHKB && roll == 6;

            if (isKB || isHKB)
            {
                if (IsKBEligible(defenderType, isHKB))
                    killingBlows++;
                else
                    nonEligKB++;
            }
            else
            {
                normalWounds++;
            }
        }

        return (totalWounds, killingBlows, nonEligKB, normalWounds);
    }

    private static int RollArmourSaves(
        int wounds, UnitConfiguration defender, ParsedSpecialRules defenderRules,
        int ap, int strengthMod,
        bool ignoresArmour, bool lightningAttacks, Random rng,
        int defenderBaseArmour)
    {
        int unsaved = 0;
        int armourSave = ignoresArmour ? 7 : defender.EffectiveArmourSaveFast(defenderBaseArmour, ap, strengthMod);

        for (int i = 0; i < wounds; i++)
        {
            if (ignoresArmour || lightningAttacks)
            {
                if (lightningAttacks && defenderRules.NaturalArmour.HasValue)
                {
                    int modNatSave = defenderRules.NaturalArmour.Value - strengthMod;
                    if (modNatSave >= 2 && rng.Next(1, 7) >= modNatSave)
                        continue;
                }
                unsaved++;
            }
            else
            {
                if (armourSave <= 6 && rng.Next(1, 7) >= armourSave)
                    continue;
                unsaved++;
            }
        }
        return unsaved;
    }

    private static int RollWardSaves(int wounds, int wardNeeded, Random rng)
    {
        if (wardNeeded > 6) return wounds;
        int unsaved = 0;
        for (int i = 0; i < wounds; i++)
        {
            if (rng.Next(1, 7) >= wardNeeded) continue;
            unsaved++;
        }
        return unsaved;
    }

    private int ResolveDefenderParry(UnitConfiguration defender, int attackerStrength, bool isMountAttack)
    {
        int parryValue = defender.SpecialRules.Parry ?? 7;

        // Weapon Parry (e.g., Two Hand Weapons: Parry 6+)
        var defenderWeapon = _coreRules.GetCloseCombatWeapon(defender.SelectedWeaponKey);
        if (defenderWeapon != null)
        {
            var defWeaponRules = ParsedWeaponRules.Parse(defenderWeapon.SpecialRules);
            if (defWeaponRules.Parry.HasValue)
                parryValue = Math.Min(parryValue, defWeaponRules.Parry.Value);
        }

        // Shield/Buckler Parry (only with hand weapon)
        if (defender.SelectedWeaponKey == "handWeapon")
        {
            foreach (var armourKey in defender.EquipmentArmour)
            {
                var armour = _coreRules.GetArmour(armourKey);
                if (armour?.SpecialRules != null)
                {
                    var parryMatch = System.Text.RegularExpressions.Regex.Match(
                        armour.SpecialRules, @"Parry\s*\((\d)\+\)");
                    if (parryMatch.Success)
                        parryValue = Math.Min(parryValue, int.Parse(parryMatch.Groups[1].Value));
                }
            }
        }

        // Parry restrictions: attacker S >= defender S+3, or defender is mounted
        int defenderS = defender.PrimaryProfile.S ?? 3;
        if (attackerStrength >= defenderS + 3 || defender.TroopType.HasSplitProfile)
            parryValue = 7;

        return parryValue;
    }

    private static int AllocateWoundsToModels(
        int kbKills, int nonEligKBHits, int normalHits,
        int defenderW, int modelCount,
        int mwValue, string? mwRoll, Random rng)
    {
        int modelsRemaining = modelCount;
        int currentModelHP = defenderW;
        int finalWounds = 0;

        // KB: each kills current model outright
        for (int i = 0; i < kbKills && modelsRemaining > 0; i++)
        {
            finalWounds += currentModelHP;
            modelsRemaining--;
            currentModelHP = defenderW;
        }

        // Non-eligible KB: 1 wound each, no Multiple Wounds
        for (int i = 0; i < nonEligKBHits && modelsRemaining > 0; i++)
        {
            int damage = Math.Min(1, currentModelHP);
            finalWounds += damage;
            currentModelHP -= damage;
            if (currentModelHP <= 0)
            {
                modelsRemaining--;
                currentModelHP = defenderW;
            }
        }

        // Normal: with Multiple Wounds if applicable
        bool hasMultipleWounds = mwValue > 1 || mwRoll != null;
        for (int i = 0; i < normalHits && modelsRemaining > 0; i++)
        {
            int damagePerHit = hasMultipleWounds
                ? (mwRoll != null ? DiceRoller.Roll(mwRoll, rng) : mwValue)
                : 1;

            int actualDamage = Math.Min(damagePerHit, currentModelHP);
            finalWounds += actualDamage;
            currentModelHP -= actualDamage;

            if (currentModelHP <= 0)
            {
                modelsRemaining--;
                currentModelHP = defenderW;
            }
        }

        return finalWounds;
    }

    private static int GetImpactHitCount(UnitConfiguration unit, Random rng)
    {
        int fromRules = 0;
        if (unit.SpecialRules.ImpactHits.HasValue)
            fromRules = unit.SpecialRules.ImpactHits.Value * unit.ModelsInFrontRank;
        else if (unit.SpecialRules.ImpactHitsRoll != null)
            fromRules = DiceRoller.Roll(unit.SpecialRules.ImpactHitsRoll, rng) * unit.ModelsInFrontRank;

        int fromType = unit.TroopType.BaseType switch
        {
            BaseTroopType.Cavalry => unit.ModelsInFrontRank,
            BaseTroopType.MonstrousCavalry => unit.ModelsInFrontRank,
            _ => 0
        };

        return Math.Max(fromRules, fromType);
    }

    private static bool IsKBEligible(TroopTypeInfo targetType, bool isHeroic)
    {
        // HKB is treated as regular KB for Swarms; works against everything else
        if (isHeroic && targetType.BaseType != BaseTroopType.Swarms)
            return true;

        return targetType.BaseType is
            BaseTroopType.Infantry or
            BaseTroopType.LargeInfantry or
            BaseTroopType.Cavalry or
            BaseTroopType.WarBeasts;
    }
}
