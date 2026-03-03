using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Calculator;

public class CombatResolver
{
    private readonly CoreRulesService _coreRules;
    private readonly AttackResolver _attackResolver;

    public CombatResolver(CoreRulesService coreRules)
    {
        _coreRules = coreRules;
        _attackResolver = new AttackResolver(coreRules);
    }

    public RoundResult ResolveRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        Random rng)
    {
        int attackerWoundsDealt = 0; // wounds dealt TO defender
        int defenderWoundsDealt = 0; // wounds dealt TO attacker

        // Phase 1: Impact Hits (charge turn only)
        if (context.Charge == ChargeState.AttackerCharged)
        {
            attackerWoundsDealt += _attackResolver.ResolveImpactHits(attacker, defender, context, rng);
        }
        else if (context.Charge == ChargeState.DefenderCharged)
        {
            defenderWoundsDealt += _attackResolver.ResolveImpactHits(defender, attacker, context, rng);
        }

        // Phase 2-3: Resolve attacks in strike order
        // Build attack groups
        var groups = BuildAttackGroups(attacker, defender, context);
        groups.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var group in groups)
        {
            var weapon = group.Weapon ?? _coreRules.GetHandWeapon();
            var result = _attackResolver.ResolveAttacks(
                group.Unit, group.Opponent, weapon, context, group.IsMountAttack, rng);

            if (group.IsAttackerSide)
                attackerWoundsDealt += result.UnsavedWounds;
            else
                defenderWoundsDealt += result.UnsavedWounds;
        }

        // Phase 4: Stomps (Always Strikes Last)
        attackerWoundsDealt += ResolveStomp(attacker, defender, context, rng);
        defenderWoundsDealt += ResolveStomp(defender, attacker, context, rng);

        // Phase 5: Combat Resolution
        var attackerBreakdown = ComputeCombatRes(attacker, defender, attackerWoundsDealt, context, true);
        var defenderBreakdown = ComputeCombatRes(defender, attacker, defenderWoundsDealt, context, false);

        int attackerCR = (int)(attackerBreakdown.Wounds + attackerBreakdown.RankBonus +
            attackerBreakdown.StandardBearer + attackerBreakdown.ChargeBonus + attackerBreakdown.FearBonus);
        int defenderCR = (int)(defenderBreakdown.Wounds + defenderBreakdown.RankBonus +
            defenderBreakdown.StandardBearer + defenderBreakdown.ChargeBonus + defenderBreakdown.FearBonus);

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

    private List<AttackGroup> BuildAttackGroups(
        UnitConfiguration attacker, UnitConfiguration defender, CombatContext context)
    {
        var groups = new List<AttackGroup>();

        // Attacker rider attacks
        var attackerWeapon = _coreRules.GetCloseCombatWeapon(attacker.SelectedWeaponKey)
            ?? _coreRules.GetHandWeapon();
        var attackerWeaponRules = ParsedWeaponRules.Parse(attackerWeapon.SpecialRules, attackerWeapon.Notes);
        int attackerI = attacker.PrimaryProfile.I ?? 1;
        attackerI += attackerWeaponRules.InitiativeMod;
        bool attackerIsCharged = context.Charge == ChargeState.DefenderCharged;
        if (attackerIsCharged)
            attackerI += attackerWeaponRules.ChargedToFrontInitiativeBonus;

        groups.Add(new AttackGroup
        {
            Unit = attacker,
            Opponent = defender,
            Weapon = attackerWeapon,
            WeaponRules = attackerWeaponRules,
            IsMountAttack = false,
            IsAttackerSide = true,
            Initiative = Math.Max(1, attackerI),
            HasASF = attacker.SpecialRules.AlwaysStrikesFirst,
            HasASL = attacker.SpecialRules.AlwaysStrikesLast
        });

        // Attacker mount attacks (if split profile)
        if (attacker.MountProfile != null && attacker.TroopType.HasSplitProfile)
        {
            groups.Add(new AttackGroup
            {
                Unit = attacker,
                Opponent = defender,
                Weapon = null, // mounts use natural weapons
                IsMountAttack = true,
                IsAttackerSide = true,
                Initiative = attacker.MountProfile.I ?? 1,
                HasASF = false,
                HasASL = false
            });
        }

        // Defender rider attacks
        var defenderWeapon = _coreRules.GetCloseCombatWeapon(defender.SelectedWeaponKey)
            ?? _coreRules.GetHandWeapon();
        var defenderWeaponRules = ParsedWeaponRules.Parse(defenderWeapon.SpecialRules, defenderWeapon.Notes);
        int defenderI = defender.PrimaryProfile.I ?? 1;
        defenderI += defenderWeaponRules.InitiativeMod;
        bool defenderIsCharged = context.Charge == ChargeState.AttackerCharged;
        if (defenderIsCharged)
            defenderI += defenderWeaponRules.ChargedToFrontInitiativeBonus;

        groups.Add(new AttackGroup
        {
            Unit = defender,
            Opponent = attacker,
            Weapon = defenderWeapon,
            WeaponRules = defenderWeaponRules,
            IsMountAttack = false,
            IsAttackerSide = false,
            Initiative = Math.Max(1, defenderI),
            HasASF = defender.SpecialRules.AlwaysStrikesFirst,
            HasASL = defender.SpecialRules.AlwaysStrikesLast
        });

        // Defender mount attacks
        if (defender.MountProfile != null && defender.TroopType.HasSplitProfile)
        {
            groups.Add(new AttackGroup
            {
                Unit = defender,
                Opponent = attacker,
                Weapon = null,
                IsMountAttack = true,
                IsAttackerSide = false,
                Initiative = defender.MountProfile.I ?? 1,
                HasASF = false,
                HasASL = false
            });
        }

        return groups;
    }

    private int ResolveStomp(UnitConfiguration stomper, UnitConfiguration target, CombatContext context, Random rng)
    {
        // Only Monstrous Infantry, Monstrous Beasts, Monstrous Cavalry, Monsters can stomp
        bool canStomp = stomper.TroopType.BaseType is
            BaseTroopType.MonstrousInfantry or
            BaseTroopType.MonstrousBeasts or
            BaseTroopType.MonstrousCavalry or
            BaseTroopType.Monster;

        if (!canStomp) return 0;

        // Can't stomp other monstrous/large targets
        bool targetEligible = target.TroopType.BaseType is
            BaseTroopType.Infantry or
            BaseTroopType.LargeInfantry or
            BaseTroopType.WarBeasts or
            BaseTroopType.Swarms;

        if (!targetEligible) return 0;

        // Stomp from special rules or troop type default
        int stompHits;
        if (stomper.SpecialRules.Stomp.HasValue)
            stompHits = stomper.SpecialRules.Stomp.Value;
        else if (stomper.SpecialRules.StompRoll != null)
            stompHits = RollDice(stomper.SpecialRules.StompRoll, rng);
        else
            stompHits = stomper.TroopType.BaseType == BaseTroopType.Monster ? RollDice("D6", rng) : 1;

        // Stomps wound using model's Strength + Mighty Blow in first round
        int strength = stomper.PrimaryProfile.S ?? 3;
        if (context.CombatRound == 1 && stomper.SpecialRules.MightyBlow.HasValue)
            strength += stomper.SpecialRules.MightyBlow.Value;
        int defenderT = (target.MountProfile != null && target.TroopType.HasSplitProfile)
            ? Math.Max(target.PrimaryProfile.T ?? 3, target.MountProfile.T ?? 3)
            : target.PrimaryProfile.T ?? 3;
        int toWoundNeeded = _coreRules.GetToWoundScore(strength, defenderT);
        int strengthMod = _coreRules.GetArmourSaveModifier(strength);

        int wounds = 0;
        for (int i = 0; i < stompHits; i++)
        {
            if (rng.Next(1, 7) < toWoundNeeded) continue;

            int armourSave = target.EffectiveArmourSave(strength, 0, strengthMod, _attackResolver.GetArmourSaveLookup());
            if (armourSave <= 6 && rng.Next(1, 7) >= armourSave) continue;

            int ward = Math.Min(target.SpecialRules.WardSave ?? 7, target.SpecialRules.Regeneration ?? 7);
            if (ward <= 6 && rng.Next(1, 7) >= ward) continue;

            wounds++;
        }

        return wounds;
    }

    private static CombatResBreakdown ComputeCombatRes(
        UnitConfiguration unit,
        UnitConfiguration opponent,
        int woundsDealt,
        CombatContext context,
        bool isAttacker)
    {
        double rankBonus = unit.RankBonus;
        double standardBearer = unit.HasStandardBearer ? 1 : 0;

        bool isCharging = (isAttacker && context.Charge == ChargeState.AttackerCharged)
            || (!isAttacker && context.Charge == ChargeState.DefenderCharged);
        double chargeBonus = isCharging ? 1 : 0;

        // Fear bonus
        double fearBonus = 0;
        if (unit.SpecialRules.Fear || unit.SpecialRules.Terror)
        {
            // Check if opponent majority causes Fear (immune to Fear CR bonus)
            bool opponentFearImmune = opponent.SpecialRules.Fear || opponent.SpecialRules.Terror;
            if (!opponentFearImmune)
            {
                fearBonus = 1;
                // +2 if Fear-causing US >= 2x enemy US in base contact
                int unitUS = unit.ModelCount * unit.TroopType.UnitStrength;
                int oppUS = opponent.ModelCount * opponent.TroopType.UnitStrength;
                if (unitUS >= 2 * oppUS)
                    fearBonus = 2;
            }
        }

        return new CombatResBreakdown
        {
            Wounds = woundsDealt,
            RankBonus = rankBonus,
            StandardBearer = standardBearer,
            ChargeBonus = chargeBonus,
            FearBonus = fearBonus
        };
    }

    private static int RollDice(string expr, Random rng)
    {
        var upper = expr.ToUpperInvariant().Trim();
        if (upper == "D6") return rng.Next(1, 7);
        if (upper == "D3") return rng.Next(1, 4);
        if (upper.StartsWith("D3+") && int.TryParse(upper[3..], out var d3mod))
            return rng.Next(1, 4) + d3mod;
        if (upper.StartsWith("D6+") && int.TryParse(upper[3..], out var d6mod))
            return rng.Next(1, 7) + d6mod;
        return 1;
    }

    private class AttackGroup
    {
        public UnitConfiguration Unit { get; init; } = null!;
        public UnitConfiguration Opponent { get; init; } = null!;
        public WeaponEntry? Weapon { get; init; }
        public ParsedWeaponRules? WeaponRules { get; init; }
        public bool IsMountAttack { get; init; }
        public bool IsAttackerSide { get; init; }
        public int Initiative { get; init; }
        public bool HasASF { get; init; }
        public bool HasASL { get; init; }

        public int Priority
        {
            get
            {
                if (HasASF && HasASL) return Initiative; // cancel out
                if (HasASF) return 100 + Initiative; // strikes first
                if (HasASL) return -100 + Initiative; // strikes last
                return Initiative;
            }
        }
    }
}
