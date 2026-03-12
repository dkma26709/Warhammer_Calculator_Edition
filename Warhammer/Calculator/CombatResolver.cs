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

    public RoundResult ResolveRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        List<AttackGroup> groups,
        Random rng)
    {
        int attackerWoundsDealt = 0;
        int defenderWoundsDealt = 0;

        // Phase 1: Impact Hits (charge turn only)
        if (context.Charge == ChargeState.AttackerCharged)
        {
            int targetBaseArmour = _attackResolver.ComputeBaseArmour(defender);
            attackerWoundsDealt += _attackResolver.ResolveImpactHits(attacker, defender, context, rng, targetBaseArmour);
        }
        else if (context.Charge == ChargeState.DefenderCharged)
        {
            int targetBaseArmour = _attackResolver.ComputeBaseArmour(attacker);
            defenderWoundsDealt += _attackResolver.ResolveImpactHits(defender, attacker, context, rng, targetBaseArmour);
        }

        // Phase 2-3: Melee attacks in strike order (groups are pre-sorted)
        foreach (var group in groups)
        {
            var weapon = group.Weapon ?? _repo.GetHandWeapon();
            var result = _attackResolver.ResolveAttacks(
                group.Unit, group.Opponent, weapon, context,
                group.IsMountAttack, group.IsAttackerSide,
                group.CachedWeaponRules, group.CachedDefenderParry,
                rng,
                group.CachedDefenderBaseArmour, group.CachedEffectiveDefenderI);

            if (group.IsAttackerSide)
                attackerWoundsDealt += result.UnsavedWounds;
            else
                defenderWoundsDealt += result.UnsavedWounds;
        }

        // Phase 3.5: Deathblow
        if (attacker.SpecialRules.Deathblow && defenderWoundsDealt > 0)
        {
            int slain = Math.Min(defenderWoundsDealt, attacker.ModelCount);
            attackerWoundsDealt += ResolveDeathblows(attacker, defender, slain, context, rng);
        }

        if (defender.SpecialRules.Deathblow && attackerWoundsDealt > 0)
        {
            int slain = Math.Min(attackerWoundsDealt, defender.ModelCount);
            defenderWoundsDealt += ResolveDeathblows(defender, attacker, slain, context, rng);
        }

        // Phase 4: Stomps
        attackerWoundsDealt += ResolveStomp(attacker, defender, context, rng);
        defenderWoundsDealt += ResolveStomp(defender, attacker, context, rng);

        // Cap wounds
        attackerWoundsDealt = Math.Min(attackerWoundsDealt, defender.ModelCount * (defender.PrimaryProfile.W ?? 1));
        defenderWoundsDealt = Math.Min(defenderWoundsDealt, attacker.ModelCount * (attacker.PrimaryProfile.W ?? 1));

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
        int effectiveI = unit.SpecialRules.Swordmaster
            ? baseI
            : weaponRules.ApplyToInitiative(baseI, isCharging, isBeingCharged);

        int defenderParry = _attackResolver.ComputeDefenderParry(opponent, unit, weapon, weaponRules, context, isAttackerSide);
        int defenderBaseArmour = _attackResolver.ComputeBaseArmour(opponent);

        // Pre-compute defender effective initiative
        var defWeapon = ResolveWeapon(opponent);
        int defenderEffI = opponent.PrimaryProfile.I ?? 1;
        if (!opponent.SpecialRules.Swordmaster)
        {
            defenderEffI = defWeapon.Rules.ApplyToInitiative(defenderEffI,
                context.IsCharging(!isAttackerSide), context.IsCharging(isAttackerSide));
        }

        groups.Add(new AttackGroup
        {
            Unit = unit,
            Opponent = opponent,
            Weapon = weapon,
            IsMountAttack = false,
            IsAttackerSide = isAttackerSide,
            Initiative = effectiveI,
            HasASF = unit.SpecialRules.AlwaysStrikesFirst || unit.SelectedShadowDance == ShadowDance.WovenMist,
            HasASL = unit.SpecialRules.AlwaysStrikesLast,
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
        int targetWS = target.BestWS;
        if (target.SpecialRules.Minus1WSToAttackers)
            targetWS = Math.Max(1, targetWS + 1);
        int toHitNeeded = _repo.GetToHitScore(deathblower.PrimaryProfile.WS ?? 1, targetWS);
        if (target.SpecialRules.Minus1ToBeHit)
            toHitNeeded = Math.Min(toHitNeeded + 1, 6);
        int strength = deathblower.PrimaryProfile.S ?? 3;
        int toWoundNeeded = _repo.GetToWoundScore(strength, target.BestT);
        int strengthMod = _repo.GetArmourSaveModifier(strength);
        int defBaseArmour = _attackResolver.ComputeBaseArmour(target);

        int wardVal = target.SpecialRules.WardSave ?? 7;
        int regenVal = target.SpecialRules.Regeneration ?? 7;
        bool flaming = deathblower.SpecialRules.FlamingAttacks;
        int ward = flaming ? wardVal : Math.Min(wardVal, regenVal);

        int wounds = 0;
        for (int i = 0; i < slainCount; i++)
        {
            int hitRoll = rng.Next(1, 7);
            if (hitRoll != 6 && (hitRoll == 1 || hitRoll < toHitNeeded)) continue;

            int woundRoll = rng.Next(1, 7);
            if (woundRoll != 6 && (woundRoll == 1 || woundRoll < toWoundNeeded)) continue;

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

        int stompHits;
        if (stomper.SpecialRules.Stomp.HasValue)
            stompHits = stomper.SpecialRules.Stomp.Value;
        else if (stomper.SpecialRules.StompRoll != null)
            stompHits = DiceRoller.Roll(stomper.SpecialRules.StompRoll, rng);
        else
            stompHits = stomper.TroopType.BaseType == BaseTroopType.Monster
                ? DiceRoller.Roll("D6", rng) : 1;

        int strength = stomper.PrimaryProfile.S ?? 3;
        if (context.CombatRound == 1 && stomper.SpecialRules.MightyBlow.HasValue)
            strength += stomper.SpecialRules.MightyBlow.Value;

        int toWoundNeeded = _repo.GetToWoundScore(strength, target.BestT);
        int strengthMod = _repo.GetArmourSaveModifier(strength);

        bool flamingStomp = stomper.SpecialRules.FlamingAttacks;
        int wardVal = target.SpecialRules.WardSave ?? 7;
        int regenVal = target.SpecialRules.Regeneration ?? 7;
        int ward = flamingStomp ? wardVal : Math.Min(wardVal, regenVal);

        if (target.SpecialRules.Ethereal)
        {
            int etherealWard = stomper.SpecialRules.MagicalAttacks ? 7 : 2;
            ward = Math.Min(ward, etherealWard);
        }

        int baseArmour = targetBaseArmour >= 0 ? targetBaseArmour : _attackResolver.ComputeBaseArmour(target);

        int wounds = 0;
        for (int i = 0; i < stompHits; i++)
        {
            if (rng.Next(1, 7) < toWoundNeeded) continue;

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

        double fearBonus = 0;
        bool causesFear = unit.SpecialRules.Fear || unit.SpecialRules.Terror;

        if (!causesFear && opponent.SpecialRules.FearElves)
        {
            string? unitFaction = isAttacker ? context.AttackerFactionKey : context.DefenderFactionKey;
            if (FactionRaces.IsElf(unitFaction))
                causesFear = true;
        }

        if (causesFear)
        {
            bool opponentImmune = opponent.SpecialRules.Fear || opponent.SpecialRules.Terror;
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
