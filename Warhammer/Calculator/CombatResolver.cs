using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class CombatResolver
{
    private readonly IGameDataRepository _repo;
    private readonly AttackResolver _attackResolver;

    public CombatResolver(IGameDataRepository repo)
    {
        _repo = repo;
        _attackResolver = new AttackResolver(repo);
    }

    /// <summary>
    /// Pre-build attack groups and cache weapon lookups. Call once before the simulation loop.
    /// </summary>
    public List<AttackGroup> PrepareAttackGroups(
        UnitConfiguration attacker, UnitConfiguration defender, CombatContext context)
    {
        return BuildAttackGroups(attacker, defender, context);
    }

    /// <summary>
    /// Resolve a single combat round. Creates internal casualty state and restores
    /// ModelCount after the round (safe for repeated single-round iterations).
    /// </summary>
    public RoundResult ResolveRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        List<AttackGroup> groups,
        Random rng)
    {
        int origAttModels = attacker.ModelCount;
        int origDefModels = defender.ModelCount;
        var attackerState = new CombatUnitState(attacker.ModelCount, attacker.PrimaryProfile.W ?? 1);
        var defenderState = new CombatUnitState(defender.ModelCount, defender.PrimaryProfile.W ?? 1);

        try
        {
            return ResolveRound(attacker, defender, context, groups, rng,
                ref attackerState, ref defenderState);
        }
        finally
        {
            attacker.ModelCount = origAttModels;
            defender.ModelCount = origDefModels;
        }
    }

    /// <summary>
    /// Resolve a single combat round with caller-provided casualty state.
    /// ModelCount on both units is updated to reflect casualties; the caller
    /// is responsible for restoring them if needed.
    /// </summary>
    public RoundResult ResolveRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        List<AttackGroup> groups,
        Random rng,
        ref CombatUnitState attackerState,
        ref CombatUnitState defenderState)
    {
        int attackerWoundsDealt = 0;
        int defenderWoundsDealt = 0;

        // Phase 1: Impact Hits (charge turn only)
        // Casualties from impact hits reduce the target's model count before melee.
        if (context.Charge == ChargeState.AttackerCharged)
        {
            int targetBaseArmour = _attackResolver.ComputeBaseArmour(defender);
            int impactWounds = _attackResolver.ResolveImpactHits(attacker, defender, context, rng, targetBaseArmour);
            int capped = Math.Min(impactWounds, defenderState.TotalRemainingHP);
            defenderState.ApplyCasualties(capped);
            attackerWoundsDealt += capped;
            defender.ModelCount = defenderState.CurrentModelCount;
        }
        else if (context.Charge == ChargeState.DefenderCharged)
        {
            int targetBaseArmour = _attackResolver.ComputeBaseArmour(attacker);
            int impactWounds = _attackResolver.ResolveImpactHits(defender, attacker, context, rng, targetBaseArmour);
            int capped = Math.Min(impactWounds, attackerState.TotalRemainingHP);
            attackerState.ApplyCasualties(capped);
            defenderWoundsDealt += capped;
            attacker.ModelCount = attackerState.CurrentModelCount;
        }

        // Phase 2-3: Melee attacks in strike order (groups are pre-sorted by Priority desc).
        // Groups at the same Priority strike simultaneously — casualties are applied
        // only between initiative steps, not within them.
        int attModelsBeforeMelee = attackerState.CurrentModelCount;
        int defModelsBeforeMelee = defenderState.CurrentModelCount;

        int gi = 0;
        while (gi < groups.Count)
        {
            int currentPriority = groups[gi].Priority;

            // Sync model counts for this initiative step
            attacker.ModelCount = attackerState.CurrentModelCount;
            defender.ModelCount = defenderState.CurrentModelCount;

            int stepAttackerWounds = 0;
            int stepDefenderWounds = 0;

            // Resolve all groups at this initiative simultaneously
            while (gi < groups.Count && groups[gi].Priority == currentPriority)
            {
                var group = groups[gi];
                gi++;

                // Skip if the attacking unit has no models left
                if (group.IsAttackerSide && attackerState.CurrentModelCount <= 0) continue;
                if (!group.IsAttackerSide && defenderState.CurrentModelCount <= 0) continue;
                // Skip if the target unit has no models left
                if (group.IsAttackerSide && defenderState.CurrentModelCount <= 0) continue;
                if (!group.IsAttackerSide && attackerState.CurrentModelCount <= 0) continue;

                var weapon = group.Weapon ?? _repo.GetHandWeapon();
                var result = _attackResolver.ResolveAttacks(
                    group.Unit, group.Opponent, weapon, context,
                    group.IsMountAttack, group.IsAttackerSide,
                    group.CachedWeaponRules, group.CachedDefenderParry,
                    rng,
                    group.CachedDefenderBaseArmour, group.CachedEffectiveDefenderI);

                if (group.IsAttackerSide)
                    stepAttackerWounds += result.UnsavedWounds;
                else
                    stepDefenderWounds += result.UnsavedWounds;
            }

            // Cap wounds at remaining HP and apply casualties from this initiative step
            int cappedAtt = Math.Min(stepAttackerWounds, defenderState.TotalRemainingHP);
            int cappedDef = Math.Min(stepDefenderWounds, attackerState.TotalRemainingHP);

            defenderState.ApplyCasualties(cappedAtt);
            attackerState.ApplyCasualties(cappedDef);

            attackerWoundsDealt += cappedAtt;
            defenderWoundsDealt += cappedDef;
        }

        // Phase 3.5: Deathblow — dying models get one last strike.
        // Use actual models killed during melee (not wounds, which may differ for multi-wound models).
        int attModelsKilledInMelee = attModelsBeforeMelee - attackerState.CurrentModelCount;
        int defModelsKilledInMelee = defModelsBeforeMelee - defenderState.CurrentModelCount;

        attacker.ModelCount = attackerState.CurrentModelCount;
        defender.ModelCount = defenderState.CurrentModelCount;

        if (attacker.EffectiveSpecialRules.Deathblow && attModelsKilledInMelee > 0)
        {
            int deathblowWounds = ResolveDeathblows(attacker, defender, attModelsKilledInMelee, context, rng);
            int capped = Math.Min(deathblowWounds, defenderState.TotalRemainingHP);
            defenderState.ApplyCasualties(capped);
            attackerWoundsDealt += capped;
            defender.ModelCount = defenderState.CurrentModelCount;
        }

        if (defender.EffectiveSpecialRules.Deathblow && defModelsKilledInMelee > 0)
        {
            int deathblowWounds = ResolveDeathblows(defender, attacker, defModelsKilledInMelee, context, rng);
            int capped = Math.Min(deathblowWounds, attackerState.TotalRemainingHP);
            attackerState.ApplyCasualties(capped);
            defenderWoundsDealt += capped;
            attacker.ModelCount = attackerState.CurrentModelCount;
        }

        // Phase 4: Stomps
        attacker.ModelCount = attackerState.CurrentModelCount;
        defender.ModelCount = defenderState.CurrentModelCount;

        int stompAttWounds = ResolveStomp(attacker, defender, context, rng);
        int stompDefWounds = ResolveStomp(defender, attacker, context, rng);

        int cappedStompAtt = Math.Min(stompAttWounds, defenderState.TotalRemainingHP);
        int cappedStompDef = Math.Min(stompDefWounds, attackerState.TotalRemainingHP);

        defenderState.ApplyCasualties(cappedStompAtt);
        attackerState.ApplyCasualties(cappedStompDef);

        attackerWoundsDealt += cappedStompAtt;
        defenderWoundsDealt += cappedStompDef;

        // Sync final model counts for CR calculation (rank bonus, fear US)
        attacker.ModelCount = attackerState.CurrentModelCount;
        defender.ModelCount = defenderState.CurrentModelCount;

        // Phase 5: Combat Resolution
        var attackerBreakdown = ComputeCombatRes(attacker, defender, attackerWoundsDealt, context, isAttacker: true);
        var defenderBreakdown = ComputeCombatRes(defender, attacker, defenderWoundsDealt, context, isAttacker: false);

        int attackerCR = attackerBreakdown.Total();
        int defenderCR = defenderBreakdown.Total();

        // Musician tiebreaker
        if (attackerCR == defenderCR)
        {
            if (attacker.HasMusician && !defender.HasMusician)
            {
                attackerBreakdown.MusicianBonus = 1;
                attackerCR += 1;
            }
            else if (defender.HasMusician && !attacker.HasMusician)
            {
                defenderBreakdown.MusicianBonus = 1;
                defenderCR += 1;
            }
        }

        return new RoundResult
        {
            AttackerWoundsDealt = attackerWoundsDealt,
            DefenderWoundsDealt = defenderWoundsDealt,
            AttackerCombatRes = attackerCR,
            DefenderCombatRes = defenderCR,
            AttackerBreakdown = attackerBreakdown,
            DefenderBreakdown = defenderBreakdown
        };
    }

    // ─── Attack Groups ─────────────────────────────────────────────

    private List<AttackGroup> BuildAttackGroups(
        UnitConfiguration attacker, UnitConfiguration defender, CombatContext context)
    {
        var groups = new List<AttackGroup>(4);

        AddRiderGroup(groups, attacker, defender, isAttackerSide: true, context);
        AddMountGroup(groups, attacker, defender, isAttackerSide: true);
        AddRiderGroup(groups, defender, attacker, isAttackerSide: false, context);
        AddMountGroup(groups, defender, attacker, isAttackerSide: false);

        groups.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        return groups;
    }

    private void AddRiderGroup(List<AttackGroup> groups,
        UnitConfiguration unit, UnitConfiguration opponent,
        bool isAttackerSide, CombatContext context)
    {
        var weapon = ResolveWeapon(unit);
        var weaponRules = weapon.Rules;

        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsCharging(!isAttackerSide);
        int baseI = unit.PrimaryProfile.I ?? 1;
        var unitRules = unit.EffectiveSpecialRules;
        int effectiveI = unitRules.Swordmaster
            ? baseI
            : weaponRules.ApplyToInitiative(baseI, isCharging, isBeingCharged, context.CombatRound);

        int defenderParry = _attackResolver.ComputeDefenderParry(opponent, unit, weapon, weaponRules, context, isAttackerSide);
        int defenderBaseArmour = _attackResolver.ComputeBaseArmour(opponent);

        // Pre-compute defender effective initiative
        var defWeapon = ResolveWeapon(opponent);
        int defenderEffI = opponent.PrimaryProfile.I ?? 1;
        if (!opponent.EffectiveSpecialRules.Swordmaster)
        {
            defenderEffI = defWeapon.Rules.ApplyToInitiative(defenderEffI,
                context.IsCharging(!isAttackerSide), context.IsCharging(isAttackerSide),
                context.CombatRound);
        }

        groups.Add(new AttackGroup
        {
            Unit = unit,
            Opponent = opponent,
            Weapon = weapon,
            IsMountAttack = false,
            IsAttackerSide = isAttackerSide,
            Initiative = effectiveI,
            HasASF = unitRules.AlwaysStrikesFirst,
            HasASL = unitRules.AlwaysStrikesLast,
            CachedWeaponRules = weaponRules,
            CachedDefenderParry = defenderParry,
            CachedDefenderBaseArmour = defenderBaseArmour,
            CachedEffectiveDefenderI = defenderEffI
        });
    }

    private void AddMountGroup(List<AttackGroup> groups,
        UnitConfiguration unit, UnitConfiguration opponent, bool isAttackerSide)
    {
        if (unit.MountProfile == null || !unit.TroopType.HasSplitProfile) return;

        groups.Add(new AttackGroup
        {
            Unit = unit,
            Opponent = opponent,
            Weapon = null,
            IsMountAttack = true,
            IsAttackerSide = isAttackerSide,
            Initiative = unit.MountProfile.I ?? 1,
            HasASF = false,
            HasASL = false,
            CachedWeaponRules = null,
            CachedDefenderParry = 7,
            CachedDefenderBaseArmour = _attackResolver.ComputeBaseArmour(opponent),
            CachedEffectiveDefenderI = opponent.PrimaryProfile.I ?? 1
        });
    }

    /// <summary>
    /// Resolve a unit's selected weapon to a domain Weapon object.
    /// Uses resolved unit path first, falls back to enum key lookup.
    /// </summary>
    private Weapon ResolveWeapon(UnitConfiguration unit)
    {
        // Try resolved weapon from Unit domain object
        var resolved = unit.GetSelectedResolvedWeapon();
        if (resolved != null) return resolved;

        // Fall back to repository lookup by enum key
        var weapon = _repo.GetWeapon(unit.SelectedWeapon);
        if (weapon != null) return weapon;

        // Final fallback: hand weapon
        return _repo.GetHandWeapon();
    }

    // ─── Deathblow ─────────────────────────────────────────────────

    private int ResolveDeathblows(UnitConfiguration deathblower, UnitConfiguration target,
        int slainCount, CombatContext context, Random rng)
    {
        var targetRules = target.EffectiveSpecialRules;
        var deathblowerRules = deathblower.EffectiveSpecialRules;

        int targetWS = target.BestWS;
        if (targetRules.Minus1WSToAttackers)
            targetWS = Math.Max(1, targetWS + 1);
        int toHitNeeded = _repo.GetToHitRequirement(deathblower.PrimaryProfile.WS ?? 1, targetWS).FirstRoll ?? 6;
        if (targetRules.Minus1ToBeHit)
            toHitNeeded = Math.Min(toHitNeeded + 1, 6);
        int strength = deathblower.PrimaryProfile.S ?? 3;
        var toWoundNeeded = _repo.GetToWoundRequirement(strength, target.BestT);
        int strengthMod = _repo.GetArmourSaveModifier(strength);

        if (!toWoundNeeded.CanSucceed) return 0;
        int defBaseArmour = _attackResolver.ComputeBaseArmour(target);

        int wardVal = targetRules.WardSave ?? 7;
        int regenVal = targetRules.Regeneration ?? 7;
        bool flaming = deathblowerRules.FlamingAttacks;
        int ward = flaming ? wardVal : Math.Min(wardVal, regenVal);

        int wounds = 0;
        for (int i = 0; i < slainCount; i++)
        {
            int hitRoll = rng.Next(1, 7);
            if (hitRoll != 6 && (hitRoll == 1 || hitRoll < toHitNeeded)) continue;

            if (!toWoundNeeded.Roll(rng).Success) continue;

            int armourSave = target.EffectiveArmourSaveFast(defBaseArmour, 0, strengthMod);
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue;

            if (ward <= 6 && rng.Next(1, 7) >= ward) continue;

            wounds++;
        }

        return wounds;
    }

    // ─── Stomps ────────────────────────────────────────────────────

    private int ResolveStomp(UnitConfiguration stomper, UnitConfiguration target,
        CombatContext context, Random rng, int targetBaseArmour = -1)
    {
        bool canStomp = stomper.TroopType.BaseType is
            BaseTroopType.MonstrousInfantry or BaseTroopType.MonstrousBeasts or
            BaseTroopType.MonstrousCavalry or BaseTroopType.Monster;
        if (!canStomp) return 0;

        bool targetEligible = target.TroopType.BaseType is
            BaseTroopType.Infantry or BaseTroopType.LargeInfantry or
            BaseTroopType.WarBeasts or BaseTroopType.Swarms;
        if (!targetEligible) return 0;

        var stomperRules = stomper.EffectiveSpecialRules;
        var targetRules = target.EffectiveSpecialRules;

        int stompHits;
        if (stomperRules.Stomp.HasValue)
            stompHits = stomperRules.Stomp.Value;
        else if (stomperRules.StompRoll != null)
            stompHits = DiceRoller.Roll(stomperRules.StompRoll, rng);
        else
            stompHits = stomper.TroopType.BaseType == BaseTroopType.Monster
                ? DiceRoller.Roll("D6", rng) : 1;

        int strength = stomper.PrimaryProfile.S ?? 3;
        if (context.CombatRound == 1 && stomperRules.MightyBlow.HasValue)
            strength += stomperRules.MightyBlow.Value;

        var toWoundNeeded = _repo.GetToWoundRequirement(strength, target.BestT);
        int strengthMod = _repo.GetArmourSaveModifier(strength);

        if (!toWoundNeeded.CanSucceed) return 0;

        bool flamingStomp = stomperRules.FlamingAttacks;
        int wardVal = targetRules.WardSave ?? 7;
        int regenVal = targetRules.Regeneration ?? 7;
        int ward = flamingStomp ? wardVal : Math.Min(wardVal, regenVal);

        if (targetRules.Ethereal)
        {
            int etherealWard = stomperRules.MagicalAttacks ? 7 : 2;
            ward = Math.Min(ward, etherealWard);
        }

        int baseArmour = targetBaseArmour >= 0 ? targetBaseArmour : _attackResolver.ComputeBaseArmour(target);

        int wounds = 0;
        for (int i = 0; i < stompHits; i++)
        {
            if (!toWoundNeeded.Roll(rng).Success) continue;

            int armourSave = target.EffectiveArmourSaveFast(baseArmour, 0, strengthMod);
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue;

            if (ward <= 6 && rng.Next(1, 7) >= ward) continue;

            wounds++;
        }

        return wounds;
    }

    // ─── Combat Resolution ─────────────────────────────────────────

    private static MutableBreakdown ComputeCombatRes(
        UnitConfiguration unit, UnitConfiguration opponent,
        int woundsDealt, CombatContext context, bool isAttacker)
    {
        bool isCharging = context.IsCharging(isAttacker);

        var unitRules = unit.EffectiveSpecialRules;
        var opponentRules = opponent.EffectiveSpecialRules;

        double fearBonus = 0;
        bool causesFear = unitRules.Fear || unitRules.Terror;

        if (!causesFear && opponentRules.FearElves)
        {
            string? unitFaction = isAttacker ? context.AttackerFactionKey : context.DefenderFactionKey;
            if (FactionRaces.IsElf(unitFaction))
                causesFear = true;
        }

        if (causesFear)
        {
            // "If the majority of the models in a unit cause Fear, they also gain the
            // Immunity (Fear) special rule, and thus the Combat Resolution bonus of their
            // Fear-causing enemies is ignored for that unit." Immunity (Psychology) grants
            // the same — it was parsed to nothing before, so 115 units conceded the bonus.
            bool opponentImmune = opponentRules.Fear || opponentRules.Terror || opponentRules.ImmunityFear;
            if (!opponentImmune)
            {
                int unitUS = unit.ModelCount * unit.TroopType.UnitStrength;
                int oppUS = opponent.ModelCount * opponent.TroopType.UnitStrength;
                fearBonus = unitUS >= 2 * oppUS ? 2 : 1;
            }
        }

        return new MutableBreakdown
        {
            Wounds = woundsDealt,
            RankBonus = unit.RankBonus,
            StandardBearer = unit.HasStandardBearer ? 1 : 0,
            ChargeBonus = isCharging ? 1 : 0,
            FearBonus = fearBonus
        };
    }

    // ─── Types ─────────────────────────────────────────────────────

    public class AttackGroup
    {
        public UnitConfiguration Unit { get; init; } = null!;
        public UnitConfiguration Opponent { get; init; } = null!;
        public Weapon? Weapon { get; init; }
        public bool IsMountAttack { get; init; }
        public bool IsAttackerSide { get; init; }
        public int Initiative { get; init; }
        public bool HasASF { get; init; }
        public bool HasASL { get; init; }
        public ParsedWeaponRules? CachedWeaponRules { get; init; }
        public int CachedDefenderParry { get; init; }
        public int CachedDefenderBaseArmour { get; init; }
        public int CachedEffectiveDefenderI { get; init; }

        public int Priority
        {
            get
            {
                if (HasASF && HasASL) return Initiative;
                if (HasASF) return 100 + Initiative;
                if (HasASL) return -100 + Initiative;
                return Initiative;
            }
        }
    }
}
