using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class CombatResolver
{
    private readonly CoreRulesService _coreRules;
    private readonly AttackResolver _attackResolver;
    private readonly Func<string, string?> _armourSaveLookup;

    public CombatResolver(CoreRulesService coreRules)
    {
        _coreRules = coreRules;
        _attackResolver = new AttackResolver(coreRules);
        _armourSaveLookup = key => coreRules.GetArmour(key)?.CombatSave;
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
            attackerWoundsDealt += _attackResolver.ResolveImpactHits(attacker, defender, context, rng, _armourSaveLookup);
        else if (context.Charge == ChargeState.DefenderCharged)
            defenderWoundsDealt += _attackResolver.ResolveImpactHits(defender, attacker, context, rng, _armourSaveLookup);

        // Phase 2-3: Melee attacks in strike order (groups are pre-sorted)
        foreach (var group in groups)
        {
            var weapon = group.Weapon ?? _coreRules.GetHandWeapon();
            var result = _attackResolver.ResolveAttacks(
                group.Unit, group.Opponent, weapon, context,
                group.IsMountAttack, group.IsAttackerSide,
                group.CachedWeaponRules, group.CachedDefenderParry,
                rng, _armourSaveLookup,
                group.CachedDefenderBaseArmour, group.CachedEffectiveDefenderI);

            if (group.IsAttackerSide)
                attackerWoundsDealt += result.UnsavedWounds;
            else
                defenderWoundsDealt += result.UnsavedWounds;
        }

        // Phase 4: Stomps
        attackerWoundsDealt += ResolveStomp(attacker, defender, context, rng);
        defenderWoundsDealt += ResolveStomp(defender, attacker, context, rng);

        // Cap wounds at each unit's total wound pool
        attackerWoundsDealt = Math.Min(attackerWoundsDealt, defender.ModelCount * (defender.PrimaryProfile.W ?? 1));
        defenderWoundsDealt = Math.Min(defenderWoundsDealt, attacker.ModelCount * (attacker.PrimaryProfile.W ?? 1));

        // Phase 5: Combat Resolution
        var attackerBreakdown = ComputeCombatRes(attacker, defender, attackerWoundsDealt, context, isAttacker: true);
        var defenderBreakdown = ComputeCombatRes(defender, attacker, defenderWoundsDealt, context, isAttacker: false);

        int attackerCR = attackerBreakdown.Total();
        int defenderCR = defenderBreakdown.Total();

        // Musician: if combat result is a draw, side with musician wins by 1
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
        var weapon = _coreRules.GetCloseCombatWeapon(unit.SelectedWeaponKey)
            ?? _coreRules.GetHandWeapon();
        var weaponRules = ParsedWeaponRules.Parse(weapon.SpecialRules, weapon.Notes);

        bool isCharging = context.IsCharging(isAttackerSide);
        bool isBeingCharged = context.IsCharging(!isAttackerSide);
        int effectiveI = weaponRules.ApplyToInitiative(unit.PrimaryProfile.I ?? 1, isCharging, isBeingCharged);

        // Pre-compute values that don't change between iterations
        int defenderParry = _attackResolver.ComputeDefenderParry(opponent, unit, weapon, weaponRules, context, isAttackerSide);
        int defenderBaseArmour = opponent.ComputeBaseArmourModifier(_armourSaveLookup);

        // Pre-compute defender effective initiative for reroll checks
        var defWeapon = _coreRules.GetCloseCombatWeapon(opponent.SelectedWeaponKey);
        int defenderEffI = opponent.PrimaryProfile.I ?? 1;
        if (defWeapon != null)
        {
            var defWepRules = ParsedWeaponRules.Parse(defWeapon.SpecialRules);
            defenderEffI = defWepRules.ApplyToInitiative(defenderEffI,
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
            HasASF = unit.SpecialRules.AlwaysStrikesFirst,
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
            CachedDefenderBaseArmour = opponent.ComputeBaseArmourModifier(_armourSaveLookup),
            CachedEffectiveDefenderI = opponent.PrimaryProfile.I ?? 1
        });
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

        int toWoundNeeded = _coreRules.GetToWoundScore(strength, target.BestT);
        int strengthMod = _coreRules.GetArmourSaveModifier(strength);

        // Ward save: flaming negates regen, ethereal applies
        bool flamingStomp = stomper.SpecialRules.FlamingAttacks;
        int wardVal = target.SpecialRules.WardSave ?? 7;
        int regenVal = target.SpecialRules.Regeneration ?? 7;
        int ward = flamingStomp ? wardVal : Math.Min(wardVal, regenVal);

        if (target.SpecialRules.Ethereal)
        {
            int etherealWard = stomper.SpecialRules.MagicalAttacks ? 7 : 2;
            ward = Math.Min(ward, etherealWard);
        }

        int wounds = 0;
        for (int i = 0; i < stompHits; i++)
        {
            if (rng.Next(1, 7) < toWoundNeeded) continue;

            int baseArmour = targetBaseArmour >= 0 ? targetBaseArmour : target.ComputeBaseArmourModifier(_armourSaveLookup);
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
        if (unit.SpecialRules.Fear || unit.SpecialRules.Terror)
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
        public WeaponEntry? Weapon { get; init; }
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
