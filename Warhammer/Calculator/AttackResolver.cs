using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class AttackResolver
{
    private readonly IGameDataRepository _repo;

    public AttackResolver(IGameDataRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Resolve all attacks from one unit against another.
    /// Accepts pre-cached weapon rules and defender parry to avoid per-iteration allocations.
    /// </summary>
    public AttackResult ResolveAttacks(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        Weapon weapon,
        CombatContext context,
        bool isMountAttack,
        bool isAttackerSide,
        ParsedWeaponRules? cachedWeaponRules,
        int cachedDefenderParry,
        Random rng,
        int cachedDefenderBaseArmour = -1,
        int cachedEffectiveDefenderI = -1)
    {
        var attackerRules = attacker.EffectiveSpecialRules;
        var defenderRules = defender.EffectiveSpecialRules;
        bool isRider = !isMountAttack;
        var aggMod = isRider ? attacker.AggregateModifier : null;
        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsCharging(!isAttackerSide);
        bool isFirstRound = context.CombatRound == 1;

        var weaponRules = isRider ? cachedWeaponRules : null;
        var profile = isMountAttack ? attacker.MountProfile! : attacker.PrimaryProfile;

        // ── Tree Whack (replaces normal attacks) ──────────────────
        if (isRider && attacker.UseTreeWhack && attackerRules.TreeWhack)
            return ResolveTreeWhacks(attacker, defender, defenderRules, rng);

        // ── Primal Fury (Beastmen) ────────────────────────────────
        bool primalFuryHatred = false;
        bool primalFuryFrenzy = false;
        if (isRider && attackerRules.PrimalFury && isFirstRound)
        {
            int d1 = rng.Next(1, 7);
            int d2 = rng.Next(1, 7);
            int ld = profile.Ld ?? 7;
            if (d1 + d2 <= ld)
            {
                primalFuryHatred = true;
                if (d1 == 1 && d2 == 1)
                    primalFuryFrenzy = true;
            }
        }

        // ── Count attacks ──────────────────────────────────────────
        int totalAttacks = CountAttacks(attacker, attackerRules, weaponRules,
            isCharging, context.CombatRound, isMountAttack, rng);
        if (primalFuryFrenzy && !attackerRules.Frenzy && isRider)
            totalAttacks += attacker.ModelsInFrontRank + Math.Min(Math.Max(0, attacker.Ranks - 1), 1) * attacker.Frontage;
        if (totalAttacks <= 0) return default;

        // ── Effective strength ─────────────────────────────────────
        int effectiveStrength = ComputeEffectiveStrength(
            attacker, defender, weapon, weaponRules, profile,
            isMountAttack, isCharging, isBeingCharged, context.CombatRound);

        // ── Roll to hit ────────────────────────────────────────────
        int defenderEffWS = defender.BestWS;
        if (defenderRules.Minus1WSToAttackers)
            defenderEffWS = Math.Max(1, defenderEffWS + 1);

        // To-hit charts are plain scores in every system, enforced when the chart is loaded,
        // so the existing clamp-at-6 arithmetic stays correct here.
        int toHitNeeded = _repo.GetToHitRequirement(profile.WS ?? 1, defenderEffWS).FirstRoll ?? 6;

        if (defenderRules.Minus1ToBeHit)
            toHitNeeded = Math.Min(toHitNeeded + 1, 6);

        int effectiveAttackerI = profile.I ?? 1;
        if (isRider && weaponRules != null)
            effectiveAttackerI = weaponRules.ApplyToInitiative(
                effectiveAttackerI, isCharging, isBeingCharged, context.CombatRound);
        int effectiveDefenderI = cachedEffectiveDefenderI >= 0
            ? cachedEffectiveDefenderI
            : ComputeEffectiveI(defender, context, !isAttackerSide);

        string? opponentFaction = context.GetOpponentFactionKey(isAttackerSide);
        bool hatredApplies = attackerRules.Hatred
            && FactionRaces.HatredApplies(attackerRules.HatredTarget, opponentFaction);
        bool effectiveASF = attackerRules.AlwaysStrikesFirst;
        bool rerollMisses = (hatredApplies && (isFirstRound || attackerRules.EternalHatred))
            || primalFuryHatred
            || (effectiveASF && !attackerRules.AlwaysStrikesLast
                && effectiveAttackerI > effectiveDefenderI);
        bool rerollHits = !effectiveASF
            && attackerRules.AlwaysStrikesLast
            && effectiveAttackerI < effectiveDefenderI;

        // ── Tier 3 roll modifiers (to-hit) ──────────────────────────
        if (aggMod?.FixedToHit is { } fixedHit)
            toHitNeeded = fixedHit;
        else if (aggMod?.ToHitModifier is { } hitMod)
            toHitNeeded = Math.Clamp(toHitNeeded + hitMod, 2, 6);
        if (aggMod is { RerollFailedToHit: true })
            rerollMisses = true;

        bool predatoryFighter = isRider && attackerRules.PredatoryFighter;
        bool rerollOnesToHit = isRider && (attackerRules.MartialProwess || attackerRules.MartialMemories);
        int hits = RollToHit(totalAttacks, toHitNeeded, rerollMisses, rerollHits, predatoryFighter, rerollOnesToHit, rng);
        if (hits == 0) return new AttackResult { TotalHits = 0 };

        // ── Roll to wound ──────────────────────────────────────────
        var toWound = _repo.GetToWoundRequirement(effectiveStrength, defender.BestT);
        int poisonMod = (isRider && attackerRules.PoisonedAttacks && !defenderRules.ImmunityPoisonedAttacks) ? 1 : 0;
        int vampiricPenalty = defenderRules.Vampiric
            && !(isRider && attackerRules.MagicalAttacks)
            && !(isRider && attackerRules.FlamingAttacks) ? 1 : 0;
        var effectiveToWound = toWound.Step(vampiricPenalty - poisonMod);

        // ── Tier 3 roll modifiers (to-wound) ─────────────────────────
        // A fixed requirement is a plain score by definition; a modifier is a step along the
        // chart's ladder, which spends a compound result's second roll first.
        if (aggMod?.FixedToWound is { } fixedWound)
            effectiveToWound = RollRequirement.Plain(fixedWound);
        else if (aggMod?.ToWoundModifier is { } woundMod)
            effectiveToWound = effectiveToWound.Step(woundMod);

        bool flamingReroll = isRider && attackerRules.FlamingAttacks && defenderRules.Flammable && !defenderRules.ImmunityFlamingAttacks;
        if (aggMod is { RerollFailedToWound: true })
            flamingReroll = true;
        bool murderousProwess = isRider && (attackerRules.MurderousProwess || attackerRules.MartialMemories);

        bool defenderImmuneKB = defenderRules.ImmunityKillingBlow;
        bool rerollSuccessfulWounds = defenderRules.RerollSuccessfulWoundsAgainst;

        var (totalWoundsRolled, killingBlows, nonEligibleKBWounds, normalWounds) =
            RollToWound(hits, effectiveToWound, flamingReroll, murderousProwess,
                isRider && attackerRules.KillingBlow && !defenderImmuneKB,
                isRider && attackerRules.HeroicKillingBlow && !defenderImmuneKB,
                defender.TroopType, rerollSuccessfulWounds, rng);

        // ── Armour saves (normal wounds only) ──────────────────────
        int ap = (isRider ? attackerRules.ArmourPiercing : null) ?? 0;
        int strengthMod = _repo.GetArmourSaveModifier(effectiveStrength);
        bool ignoresArmour = isRider && attackerRules.IgnoresArmourSaves;
        bool lightningAttacks = isRider && attackerRules.LightningAttacks;

        int defBaseArmour = cachedDefenderBaseArmour >= 0
            ? cachedDefenderBaseArmour
            : ComputeBaseArmour(defender);
        int unsavedNormal = RollArmourSaves(normalWounds, defender, defenderRules,
            ap, strengthMod, ignoresArmour, lightningAttacks, rng, defBaseArmour);

        // ── Ward saves ─────────────────────────────────────────────
        int wardValue = defenderRules.WardSave ?? 7;
        int regenValue = defenderRules.Regeneration ?? 7;
        int parryValue = cachedDefenderParry;
        bool flamingNegatesRegen = isRider && attackerRules.FlamingAttacks && !defenderRules.ImmunityFlamingAttacks;

        int dodgeValue = defenderRules.Dodge ?? 7;

        if (defenderRules.ElvenGrace && dodgeValue > 6)
        {
            bool attackerStrikesFirst = effectiveAttackerI > effectiveDefenderI
                || (effectiveASF && !attackerRules.AlwaysStrikesLast);
            if (!attackerStrikesFirst)
                dodgeValue = Math.Min(dodgeValue, 6);
        }

        if (defenderRules.ForestSpirit)
        {
            int spiritWard = (isRider && attackerRules.MagicalAttacks) ? 6 : 5;
            wardValue = Math.Min(wardValue, spiritWard);
        }

        if (defenderRules.BlessingOfTheLady && !(isRider && attackerRules.MagicalAttacks))
            wardValue = Math.Min(wardValue, 5);

        if (defenderRules.Ethereal)
        {
            int etherealWard = (isRider && attackerRules.MagicalAttacks) ? 7 : 2;
            wardValue = Math.Min(wardValue, etherealWard);
        }

        int kbUnsaved = RollWardSaves(killingBlows, wardValue, rng);
        int nonEligKBUnsaved = RollWardSaves(nonEligibleKBWounds, Math.Min(wardValue, parryValue), rng);

        int effectiveWard = flamingNegatesRegen
            ? Math.Min(Math.Min(wardValue, parryValue), dodgeValue)
            : Math.Min(Math.Min(Math.Min(wardValue, regenValue), parryValue), dodgeValue);
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
        int targetBaseArmour)
    {
        int impactHits = GetImpactHitCount(charger, rng);
        if (impactHits <= 0) return 0;

        var chargerRules = charger.EffectiveSpecialRules;
        var targetRules = target.EffectiveSpecialRules;

        int strength = charger.MountProfile?.S ?? charger.PrimaryProfile.S ?? 3;
        if (context.CombatRound == 1 && chargerRules.MightyBlow.HasValue)
            strength += chargerRules.MightyBlow.Value;

        if (chargerRules.OgreCharge || chargerRules.LanceFormation)
            strength += charger.RankBonus;

        if (chargerRules.Impale)
            strength += 1;

        var toWoundNeeded = _repo.GetToWoundRequirement(strength, target.BestT);
        int strengthMod = _repo.GetArmourSaveModifier(strength);

        if (!toWoundNeeded.CanSucceed) return 0;

        bool flamingImpact = chargerRules.FlamingAttacks;
        int baseWard = targetRules.WardSave ?? 7;
        int baseRegen = targetRules.Regeneration ?? 7;
        int impactWard = flamingImpact ? baseWard : Math.Min(baseWard, baseRegen);

        if (targetRules.Ethereal)
        {
            int etherealWard = chargerRules.MagicalAttacks ? 7 : 2;
            impactWard = Math.Min(impactWard, etherealWard);
        }

        int wounds = 0;
        for (int i = 0; i < impactHits; i++)
        {
            if (!toWoundNeeded.Roll(rng).Success) continue;

            int armourSave = target.EffectiveArmourSaveFast(targetBaseArmour, 0, strengthMod);
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue;

            if (impactWard <= 6 && rng.Next(1, 7) >= impactWard) continue;

            wounds++;
        }

        return wounds;
    }

    /// <summary>
    /// Compute defender parry value for a given attacker weapon/rules combination.
    /// </summary>
    public int ComputeDefenderParry(
        UnitConfiguration defender,
        UnitConfiguration attacker,
        Weapon attackerWeapon,
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

    /// <summary>Compute base armour modifier for a unit from resolved armour data.</summary>
    public int ComputeBaseArmour(UnitConfiguration unit)
    {
        return unit.ComputeBaseArmourModifier();
    }

    // ─── Private helpers ───────────────────────────────────────────

    private int ComputeEffectiveI(UnitConfiguration unit, CombatContext context, bool isAttackerSide)
    {
        int baseI = unit.PrimaryProfile.I ?? 1;

        // Try resolved weapon first
        var resolvedWeapon = unit.GetSelectedResolvedWeapon();
        if (resolvedWeapon != null)
        {
            return resolvedWeapon.Rules.ApplyToInitiative(baseI,
                context.IsCharging(isAttackerSide),
                context.IsCharging(!isAttackerSide),
                context.CombatRound);
        }

        // Legacy path: lookup by string key
        var weapon = _repo.GetWeapon(unit.SelectedWeapon);
        if (weapon != null)
        {
            return weapon.Rules.ApplyToInitiative(baseI,
                context.IsCharging(isAttackerSide),
                context.IsCharging(!isAttackerSide),
                context.CombatRound);
        }

        return baseI;
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
        Weapon weapon, ParsedWeaponRules? weaponRules,
        ProfileEntry profile, bool isMountAttack,
        bool isCharging, bool isBeingCharged, int combatRound)
    {
        if (isMountAttack) return profile.S ?? 3;

        int weaponMB = 0;
        if (weaponRules?.MightyBlow != null)
        {
            bool applyWeaponMB = ParsedWeaponRules.Holds(
                weaponRules.MightyBlowCondition, isCharging, isBeingCharged, combatRound);

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

    private static int RollToHit(int attacks, int needed, bool rerollMisses, bool rerollHits,
        bool predatoryFighter, bool rerollOnesToHit, Random rng)
    {
        int hits = 0;
        int bonusAttacks = 0;

        for (int i = 0; i < attacks; i++)
        {
            int roll = rng.Next(1, 7);
            bool hit = roll == 6 || (roll != 1 && roll >= needed);
            bool wasNatural6 = roll == 6;

            if (!hit && rerollMisses)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
                wasNatural6 = roll == 6;
            }
            else if (!hit && roll == 1 && rerollOnesToHit)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
                wasNatural6 = roll == 6;
            }
            else if (hit && rerollHits)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
                wasNatural6 = roll == 6;
            }

            if (hit)
            {
                hits++;
                // wasNatural6 is refreshed in every re-roll branch above: the effect depends
                // on the roll that stands, not the one originally thrown.
                if (predatoryFighter && wasNatural6)
                    bonusAttacks++;
            }
        }

        for (int i = 0; i < bonusAttacks; i++)
        {
            int roll = rng.Next(1, 7);
            bool hit = roll == 6 || (roll != 1 && roll >= needed);

            if (!hit && rerollMisses)
            {
                roll = rng.Next(1, 7);
                hit = roll == 6 || (roll != 1 && roll >= needed);
            }
            else if (!hit && roll == 1 && rerollOnesToHit)
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
        int hits, RollRequirement toWoundNeeded, bool flamingReroll, bool rerollOnes,
        bool hasKB, bool hasHKB, TroopTypeInfo defenderType,
        bool rerollSuccessfulWounds, Random rng)
    {
        int totalWounds = 0, killingBlows = 0, nonEligKB = 0, normalWounds = 0;

        if (!toWoundNeeded.CanSucceed)
            return (0, 0, 0, 0);

        for (int i = 0; i < hits; i++)
        {
            var (wounded, roll) = toWoundNeeded.Roll(rng);

            if (!wounded && roll == 1 && rerollOnes)
            {
                (wounded, roll) = toWoundNeeded.Roll(rng);
            }
            else if (!wounded && flamingReroll)
            {
                (wounded, roll) = toWoundNeeded.Roll(rng);
            }

            if (!wounded) continue;

            if (rerollSuccessfulWounds)
            {
                (wounded, roll) = toWoundNeeded.Roll(rng);
                if (!wounded) continue;
            }

            totalWounds++;

            // Any effect that depends on a roll reads the result after re-rolls, never the
            // die originally thrown — so `roll` must stay the reassigned value above.
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
        int parryValue = defender.EffectiveSpecialRules.Parry ?? 7;

        // Weapon Parry — use resolved weapon if available, fall back to enum lookup
        var defenderWeapon = defender.GetSelectedResolvedWeapon()
            ?? _repo.GetWeapon(defender.SelectedWeapon);
        if (defenderWeapon != null && defenderWeapon.Rules.Parry.HasValue)
            parryValue = Math.Min(parryValue, defenderWeapon.Rules.Parry.Value);

        // Shield/Buckler Parry (only with hand weapon)
        // Uses EffectiveArmour (base + upgrades) so an upgrade-added shield is detected.
        if (defender.SelectedWeapon == CloseCombatWeaponKey.HandWeapon)
        {
            foreach (var armourKey in defender.EffectiveArmour)
            {
                if (defender.ResolvedUnit.AvailableArmour.TryGetValue(armourKey, out var armour)
                    && armour.ParryValue.HasValue)
                {
                    parryValue = Math.Min(parryValue, armour.ParryValue.Value);
                }
            }
        }

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

        for (int i = 0; i < kbKills && modelsRemaining > 0; i++)
        {
            finalWounds += currentModelHP;
            modelsRemaining--;
            currentModelHP = defenderW;
        }

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
        var rules = unit.EffectiveSpecialRules;
        int fromRules = 0;
        if (rules.ImpactHits.HasValue)
            fromRules = rules.ImpactHits.Value * unit.ModelsInFrontRank;
        else if (rules.ImpactHitsRoll != null)
            fromRules = DiceRoller.Roll(rules.ImpactHitsRoll, rng) * unit.ModelsInFrontRank;

        if (rules.OgreCharge)
            fromRules = Math.Max(fromRules, unit.ModelsInFrontRank);

        // wap-v2 gives Cavalry and Monstrous Cavalry Impact Hits (1) from the troop type;
        // wap-v3 removed it, so a v3 cavalry unit only has impact hits if its own entry says so.
        int fromType = unit.TroopType.InherentImpactHits * unit.ModelsInFrontRank;

        return Math.Max(fromRules, fromType);
    }

    private static AttackResult ResolveTreeWhacks(
        UnitConfiguration attacker, UnitConfiguration defender,
        ParsedSpecialRules defenderRules, Random rng)
    {
        int models = attacker.ModelsInFrontRank;
        int defenderI = defender.PrimaryProfile.I ?? 1;
        int wardValue = defenderRules.WardSave ?? 7;
        int regenValue = defenderRules.Regeneration ?? 7;
        int effectiveWard = Math.Min(wardValue, regenValue);

        int totalWounds = 0;
        int totalHits = 0;

        for (int i = 0; i < models; i++)
        {
            int initRoll = rng.Next(1, 7);
            if (initRoll <= defenderI) continue;

            totalHits++;
            int damage = rng.Next(1, 7);

            int unsaved = 0;
            for (int w = 0; w < damage; w++)
            {
                if (effectiveWard <= 6 && rng.Next(1, 7) >= effectiveWard) continue;
                unsaved++;
            }

            totalWounds += unsaved;
        }

        return new AttackResult
        {
            TotalHits = totalHits,
            TotalWoundsInflicted = totalHits,
            UnsavedWounds = totalWounds
        };
    }

    private static bool IsKBEligible(TroopTypeInfo targetType, bool isHeroic)
    {
        if (isHeroic && targetType.BaseType != BaseTroopType.Swarms)
            return true;

        return targetType.BaseType is
            BaseTroopType.Infantry or
            BaseTroopType.LargeInfantry or
            BaseTroopType.Cavalry or
            BaseTroopType.WarBeasts;
    }
}
