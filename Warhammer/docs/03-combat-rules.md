# WAP Combat Rules Specification

## Rules Source Constraint

**All rules in this document come EXCLUSIVELY from:**
1. `core-rules-new.json` — extracted from "Warhammer: The Game of Fantasy Battles - 9th Edition v2.32" PDF
2. `wap-rules.md` — user-verified rule interpretations

**No rules from LLM knowledge, web searches, or non-WAP sources.** If a rule is ambiguous, it is flagged with `[VERIFY]` for user review.

## Close Combat Sequence (Iteration 1 Scope)

A single round of close combat between two units proceeds as follows:

### 1. Determine Who Strikes First
- By default, models strike in Initiative order (highest first)
- **Always Strikes First**: Strikes before all others regardless of Initiative. If opponent also has ASF, compare Initiative normally. If model's Initiative > opponent's, re-roll failed To Hit.
- **Always Strikes Last**: Strikes after all others regardless of Initiative. If opponent also has ASL, compare Initiative normally. If model's Initiative < opponent's, re-roll successful To Hit.
- If ASF and ASL apply to the same model, both cancel out — use normal Initiative.
- **Impact Hits**: Resolved at the very beginning, before any other attacks (charge turn only).
- **Stomp**: Resolved at the end (has Always Strikes Last).

### 2. Determine Number of Attacks
- Front rank models: full Attacks characteristic
- Supporting ranks: limited attacks per troop type (see Troop Type Rules below)
- **Frenzy**: +1 Attack (from `core-rules-new.json` and `wap-rules.md`)
- **Devastating Charge**: +1 Attack on charge turn
- **Random Attacks**: Roll indicated dice to determine attacks
- **Fight in Extra Ranks (X)**: Supporting attacks from X additional ranks beyond normal

### 3. Determine Who Can Fight (Base Contact)
- Only models in base contact with the enemy can attack
- Corner-to-corner counts as base contact
- Frontage formula: `min(attackerFiles, ceil(defenderWidthMm / attackerBaseWidthMm) + 2)`
- Supporting ranks limited to the same fighting frontage

### 4. Roll To Hit
- Look up score needed: `closeCombatToHit.table[attackerWS - 1][defenderWS - 1]`
- Natural 1 always misses, natural 6 always hits
- **Hatred**: Re-roll all failed To Hit rolls in first round of combat. Dual-source: re-roll every round.
- **Always Strikes First** (if model I > opponent I): re-roll failed To Hit
- **Always Strikes Last** (if model I < opponent I): re-roll successful To Hit

### 5. Roll To Wound
- Look up score needed: `toWound.table[strength - 1][toughness - 1]`
- **Poisoned Attacks**: +1 modifier to To Wound roll. Dual-source: also auto-wounds on To Hit roll of 6.
- **Mighty Blow (X)**: +X Strength in first round of combat
- **Flammable**: If attacked with Flaming Attacks, attacker re-rolls failed To Wound
- **Killing Blow**: On natural 6 to wound, model is slain outright (see Killing Blow section below)

### 6. Armour Saves
- Base armour from equipment (light +1, medium +2, heavy +3, shield +1, barding +1)
- **Natural Armour (X+)**: Adds armour save value. Stacks with equipment.
- Strength modifier: `armourSaves.strengthModifiers["S{strength}"]` (negative values = worse save)
- **Armour Piercing (X)**: Additional negative save modifier of -X. Cumulative with other AP.
- **Killing Blow wounds**: No armour save allowed
- **Ignores Armour Saves**: No armour save allowed
- **Lightning Attacks**: Ignores armour saves except Natural Armour
- **Parry (X+)**: Ward save in close combat (front arc only, not if attacker S is +3 over model S)

### 7. Ward Saves / Regeneration
- **Ward Save (X+)**: After armour save fails, roll ward save. NOT negated by Flaming or KB.
- **Regeneration (X+)**: Grants a Ward Save. **Negated by** Flaming Attacks, Killing Blow (including Heroic KB), and Unstable wounds. If model has both Ward Save and Regeneration, use the better value (don't stack).
- **Dodge (X+)**: Ward save against attacks to front arc. Cumulative with other Dodge. Cannot be used vs Magical Attacks that hit automatically, attacks with ASF, or if model has ASL.

### 8. Apply Wounds
- Each failed save = 1 wound (unless Multiple Wounds applies)
- **Multiple Wounds (X)**: Each unsaved wound becomes X wounds. Roll separately for dice-based values (e.g., D3).
- Remove casualties from rear of unit

### 9. Combat Resolution
- **Wounds caused** by each side
- **Rank bonus**: +1 per complete rank behind the first (max +3). See Troop Type Rules for minimum rank width.
- **Standard bearer**: +1 if unit has a standard bearer
- **Charge bonus**: +1 if the unit charged this turn
- **Fear**: +1 combat resolution if unit contains Fear-causing models. +2 if Fear-causing models' Unit Strength is double or more the total Unit Strength of all enemy in base contact. (From `core-rules-new.json` fear rule.)

### 10. Determine Winner
- Side with higher combat resolution wins
- Equal = draw
- Loser must take a Break test (Iteration 3 scope — for now, just report the difference)

## Special Rules — Combat Affecting (Detailed)

All descriptions sourced from `core-rules-new.json commonSpecialRules` unless noted.

### Killing Blow
- Natural 6 To Wound = model slain outright (remaining wounds removed)
- **Ignores**: Armour saves AND Regeneration
- **Allows**: Ward saves (taken as normal)
- **Eligible targets**: Infantry, Cavalry, War Beasts only
- **Non-eligible targets**: Inflicts 1 Wound ignoring armour saves and Regeneration
- Does NOT trigger if attack wounds automatically
- **Dual-source**: Triggers on 5+ instead of 6
- **Combat resolution**: KB scores wounds equal to slain model's remaining wounds

### Heroic Killing Blow
- Same as Killing Blow but works against all troop types except Swarms
- Models with Immunity (Killing Blow) treat Heroic KB as normal Killing Blow

### Poisoned Attacks
- +1 modifier to To Wound rolls
- Armour saves modified by Strength as normal
- **Dual-source**: Also auto-wounds on To Hit roll of 6

### Regeneration (X+)
- Grants a Ward Save of the indicated value
- **IS a Ward Save** — take best of Ward Save or Regeneration, don't stack (per `wap-rules.md`)
- Cumulative with other Regeneration sources
- **Negated by**: Flaming Attacks, Killing Blow (including Heroic KB), Unstable wounds

### Ward Save (X+)
- After-armour save
- **NOT negated** by Flaming Attacks or Killing Blow (per `wap-rules.md`)

### Hatred
- Re-roll all failed To Hit in first round of close combat
- **Dual-source**: Re-roll every round, must always pursue
- Must re-roll successful restraint from pursuit vs hated enemy

### Frenzy
- +1 Attack
- Immunity (Psychology)
- Must pursue and overrun, cannot flee as charge reaction
- **Loses Frenzy if beaten in combat**
- **Dual-source**: +1 Attack (cumulative), must always pursue

### Flaming Attacks
- Wounds count as Flaming
- **Negates Regeneration** saves
- **Does NOT negate** Ward Saves (per `wap-rules.md`)
- Causes Fear in War Beasts, Cavalry, Chariots, Flammable creatures

### Armour Piercing (X)
- Additional -X armour save modifier beyond Strength penalties
- Cumulative with other AP sources
- Only close combat attacks unless specified

### Always Strikes First
- Strike first regardless of Initiative
- If opponent also has ASF: compare Initiative normally
- If model's Initiative > opponent's: re-roll failed To Hit
- If model has both ASF and ASL: both cancel out

### Always Strikes Last
- Strike last regardless of Initiative
- If opponent also has ASL: compare Initiative normally
- If model's Initiative < opponent's: re-roll successful To Hit (penalty)
- If model has both ASF and ASL: both cancel out

### Mighty Blow (X)
- +X Strength to all close combat attacks (including Impact Hits, Stomps) in **first round**
- Cumulative with other Mighty Blow sources

### Impact Hits (X)
- Charge turn only, resolved before all other attacks
- Hit automatically (randomised if in contact with multiple units)
- Resolved at model's Strength
- Parry saves cannot be taken
- Special rules from model's normal attacks do NOT apply to Impact Hits (unless specified)

### Stomp (X)
- Additional X automatic hits, resolved at model's Strength
- Always Strikes Last
- Only against models with Line of Sight ≤ 1 (≤ 2 if stomper is a Monster)
- Special rules from model's normal attacks do NOT apply to Stomps (unless specified)

### Multiple Wounds (X)
- Each unsaved wound becomes X wounds
- Model cannot take more wounds than remaining on profile
- Roll separately for each unsaved wound if X is a dice value

### Fight in Extra Ranks (X)
- Supporting attacks from X additional ranks beyond normal
- Cumulative with other sources

### Natural Armour (X+)
- Armour save equal to X+. Stacks with equipment armour.
- Stacks with other Natural Armour sources (per `core-rules-new.json`)
- Implementation: value contributes to total armour bonus like equipment

### Devastating Charge
- +1 Attack on charge turn

### Ignores Armour Saves
- All armour saves ignored. Close combat only unless specified.

### Lightning Attacks
- Ignores Armour Saves except Natural Armour
- Both shooting and close combat unless specified

### Magical Attacks
- All attacks from spells and magic items are magical
- Relevant for Ethereal models (different Ward Save vs magical vs mundane)

### Ethereal
- Ward Save (2+) against mundane attacks
- Ward Save (5+) against magical attacks
- Has Magical Attacks and Unstable

### Fear
- +1 to Combat Resolution if unit contains Fear-causing models
- +2 if Fear-causing Unit Strength ≥ 2× enemy Unit Strength in base contact
- If majority of models in a unit cause Fear → Immunity (Fear), ignoring enemy Fear combat res bonus

### Immunity (X)
- Ignores the effects of the named rule
- Immunity (Psychology) = Immune to Panic, Fear, Terror; auto-pass Psychology tests

### Stubborn
- Always Steadfast (regardless of ranks or disruption)
- Relevant for Break tests (Iteration 3)

### Unbreakable
- Immunity (Psychology), auto-pass Break tests
- Cannot flee as charge reaction

### Unstable
- Unbreakable, but suffers 1 extra wound per point lost in combat (no saves)
- If would be Steadfast: suffers 1 fewer wound
- Relevant for undead/daemon units

## Troop Type Rules (Combat-Relevant)

From `core-rules-new.json troopTypes`.

### Complete Rank Width (minimum models for rank bonus)
| Troop Type | Min Width |
|------------|-----------|
| Infantry | 5 |
| Large Infantry | 4 |
| Monstrous Infantry | 3 |
| Cavalry | 5 |
| Monstrous Cavalry | 3 |
| Swarms | 3 |
| War Beasts | 5 |
| Monstrous Beasts | 3 |
| Chariots, Monsters, Shrines, War Machines | No rank bonus |

### Supporting Attacks (max per supporting model)
| Troop Type | Max Supporting Attacks |
|------------|-----------------------|
| Infantry | 1 |
| Large Infantry | 2 |
| Monstrous Infantry | 2 |
| Cavalry (split profile) | 1 rider + 1 mount |
| Cavalry (no split profile) | 2 |
| Monstrous Cavalry (split profile) | 1 rider + 2 mount |
| Monstrous Cavalry (no split profile) | 3 |
| Swarms | 5 |
| War Beasts | 1 |
| Monstrous Beasts | 2 |

### Unit Strength
| Troop Type | US |
|------------|-----|
| Infantry | 1 |
| Large Infantry | 1 |
| Monstrous Infantry | 2 |
| Cavalry | 2 |
| Monstrous Cavalry | 3 |
| Swarms | 3 |
| War Beasts | 1 |
| Monstrous Beasts | 2 |

### Inherent Special Rules by Troop Type
| Troop Type | Inherent Rules |
|------------|----------------|
| Cavalry | Impact Hits (1) at mount's Strength, Swiftstride |
| Monstrous Cavalry | Fear, Impact Hits (1), Swiftstride |
| Monstrous Infantry | Fear, Swiftstride |
| Monstrous Beasts | Fear, Swiftstride |
| Swarms | Expendable, Unstable |
| War Beasts | Expendable, Swiftstride |

### Split Profile (Cavalry/Monstrous Cavalry)
- Rider and mount attack independently (own WS, S, I, A)
- When attacked: use highest WS, T, W from rider or mount
- Equipment/magic items apply to rider only (unless specified)

## Combat Table Reference

### To-Hit (WS vs WS)
10×10 matrix in `closeCombatToHit.table`. Row = attacker WS (1-10), Column = defender WS (1-10). Value = D6 score needed.

### To-Wound (S vs T)
10×10 matrix in `toWound.table`. Row = attacker Strength (1-10), Column = target Toughness (1-10). Value = D6 score needed.

### Armour Save Modifiers
| Strength | Modifier |
|----------|----------|
| 1-3 | 0 (no penalty) |
| 4 | -1 |
| 5 | -2 |
| 6 | -3 |
| 7 | -4 |
| 8 | -5 |
| 9 | -6 |
| 10 | -7 |

## Weapon Profiles (Close Combat)

From `core-rules-new.json weapons.closeCombat`:

| Weapon | Strength | Special Rules |
|--------|----------|---------------|
| Hand Weapon | As user | (default, can't use if other CC weapon available) |
| Two/Additional Hand Weapons | As user | +1 Attack, Parry (6+), Requires Two Hands |
| Polearm | +1 | +1 Initiative, Requires Two Hands |
| Great Weapon | +1 | -2 Initiative, Mighty Blow (1), Requires Two Hands |
| Flail | As user | Mighty Blow (2), Requires Two Hands |
| Spear | As user | +1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)* |
| Pike | As user | +2 Initiative, Fight in Extra Ranks (3), Mighty Blow (1)*, Requires Two Hands |
| Light Lance | As user | +1 Initiative*, Mighty Blow (1)* (mounted, charge only) |
| Heavy Lance | As user | +2 Initiative*, Mighty Blow (2)* (mounted, charge only) |

*Notes from extraction:*
- Spear/Pike: On charge to front, additional +1 Initiative. Mighty Blow only on charge to front. Mighty Blow doesn't apply vs Infantry, Large Infantry, Swarms, Shrines.
- Light/Heavy Lance: Mounted only. Only on charge turn, only vs charged unit.
- Spear: Model on foot only.

## Armour Profiles

From `core-rules-new.json armour`:

| Armour | Save Bonus | Special Rules |
|--------|------------|---------------|
| Light Armour | +1 (6+) | — |
| Medium Armour | +2 (5+) | — |
| Heavy Armour | +3 (4+) | — |
| Shields | +1 (6+) | Parry (6+). +1 vs missiles to front (foot only). |
| Buckler | +1 (6+) | Parry (6+). +1 vs missiles to front (foot only). |
| Barding | +1 (6+) | -1 Movement (mount) |

## Edge Cases & Interactions

These are derived from the rule descriptions above:

1. **Flaming + Regeneration**: Flaming Attacks negate Regeneration saves. Ward Saves still apply.
2. **Killing Blow + Ward Save**: KB ignores armour and Regeneration. Ward Save taken as normal.
3. **Killing Blow + non-eligible target**: Inflicts 1 wound ignoring armour and Regen (not instakill).
4. **ASF vs ASF**: Compare Initiative normally.
5. **ASF + ASL on same model**: Both cancel, use normal Initiative.
6. **Poison + Poison (dual source)**: +1 to wound AND auto-wound on To Hit roll of 6.
7. **Hatred + Hatred (dual source)**: Re-roll every round, must always pursue.
8. **Frenzy + Frenzy (dual source)**: +1 Attack (additional), must always pursue.
9. **KB + KB (dual source)**: Triggers on 5+ instead of 6.
10. **Mighty Blow + Mighty Blow**: Cumulative (add both values).
11. **Multiple sources of Impact Hits**: Use highest single value, don't add.
12. **Natural 1 always misses To Hit, natural 6 always hits** (regardless of modifiers).
13. **Impact Hits/Stomps**: Model's special rules do NOT apply to these unless specified.
14. **Ethereal vs mundane**: Ward Save 2+. Ethereal vs magical: Ward Save 5+.
15. **Lightning Attacks**: Ignore armour EXCEPT Natural Armour.

## Flagged for Verification

- `[VERIFY]` **Chaos Armour**: Per `wap-rules.md`, +1 to armour save when wearing light/medium/heavy armour. Not found as a common special rule in `core-rules-new.json` — may be faction-specific (Warriors of Chaos). Confirm this is implemented as a special rule modifier, not an equipment piece.
- `[VERIFY]` **Champion in WAP**: Per `wap-rules.md`, no extra attacks. Confirm champions provide no combat stat changes (only tactical bonuses like re-rolls).

---

# Pass 2 — Implementation Detail

## Algorithm: `CombatResolver.ResolveRound()`

```pseudo
function ResolveRound(attacker, defender, context, rng):
    attackerWounds = 0
    defenderWounds = 0

    // --- Phase 1: Impact Hits (charge turn only) ---
    if context.charge == AttackerCharged:
        attackerWounds += ResolveImpactHits(attacker, defender, rng)
    elif context.charge == DefenderCharged:
        defenderWounds += ResolveImpactHits(defender, attacker, rng)

    // --- Phase 2: Determine strike order ---
    // Build list of attack groups with their initiative values
    groups = BuildAttackGroups(attacker, defender, context)
    // Sort: ASF first, then by Initiative desc, ASL last
    // Within same priority: simultaneous (both resolve before removing casualties)
    sortedGroups = SortByStrikeOrder(groups)

    // --- Phase 3: Resolve attacks in order ---
    for each group in sortedGroups:
        wounds = ResolveAttackGroup(group, rng)
        if group.side == Attacker:
            defenderWounds += wounds
        else:
            attackerWounds += wounds

    // --- Phase 4: Stomps ---
    if attacker has Stomp and defender eligible (LoS check):
        defenderWounds += ResolveStomp(attacker, defender, rng)
    if defender has Stomp and attacker eligible:
        attackerWounds += ResolveStomp(defender, attacker, rng)

    // --- Phase 5: Combat Resolution ---
    attackerCR = ComputeCombatRes(attacker, defenderWounds, context, isAttacker=true)
    defenderCR = ComputeCombatRes(defender, attackerWounds, context, isAttacker=false)

    return RoundResult(attackerWounds, defenderWounds, attackerCR, defenderCR, ...)
```

## Algorithm: `ResolveAttackGroup()` (one unit's attacks)

```pseudo
function ResolveAttackGroup(group, rng):
    unit = group.unit
    opponent = group.opponent
    rules = unit.SpecialRules
    weapon = unit.SelectedWeapon
    context = group.context

    isCharging = (context.charge matches this unit)
    isFirstRound = (context.combatRound == 1)

    // --- Count attacks ---
    frontRankModels = unit.ModelsInBaseContact(opponent)
    supportingRanks = unit.SupportingRankCount()
    supportingAttacksPerModel = unit.TroopType.MaxSupportingAttacks
    if rules.FightInExtraRanks != null:
        supportingRanks = min(supportingRanks, normalSupportRanks + rules.FightInExtraRanks)

    attacksPerFrontModel = unit.PrimaryProfile.A ?? 1
    if rules.Frenzy: attacksPerFrontModel += 1
    if rules.DevastatingCharge and isCharging: attacksPerFrontModel += 1

    totalAttacks = frontRankModels * attacksPerFrontModel
                 + supportingRanks * frontRankModels * supportingAttacksPerModel

    // --- Determine effective Strength ---
    baseStrength = unit.PrimaryProfile.S
    weaponStrengthMod = ParseWeaponStrength(weapon)  // "+1" -> 1, "As user" -> 0
    mightyBlow = 0
    if isFirstRound:
        mightyBlow += rules.MightyBlow ?? 0
        mightyBlow += ParseWeaponMightyBlow(weapon, context)  // weapon conditional MB
    effectiveStrength = baseStrength + weaponStrengthMod + mightyBlow

    // --- Roll To Hit ---
    toHitNeeded = CoreRules.GetToHitScore(unit.WS, opponent.WS)
    hasHatredReroll = rules.Hatred and (isFirstRound or rules.HatredDualSource)
    hasASFReroll = rules.ASF and unit.I > opponent.I
    hasASLPenalty = rules.ASL and unit.I < opponent.I

    hits = 0
    poisonAutoWounds = 0
    for i in 1..totalAttacks:
        roll = rng.D6()
        hit = (roll >= toHitNeeded) or (roll == 6)
        miss = (roll == 1) or (not hit)

        if miss and (hasHatredReroll or hasASFReroll):
            roll = rng.D6()
            hit = (roll >= toHitNeeded) or (roll == 6)
        elif hit and hasASLPenalty:
            roll = rng.D6()
            hit = (roll >= toHitNeeded) or (roll == 6)

        if hit:
            hits += 1
            if rules.PoisonedAttacksDualSource and roll == 6:
                poisonAutoWounds += 1  // auto-wound, skip to-wound

    // --- Roll To Wound ---
    toWoundNeeded = CoreRules.GetToWoundScore(effectiveStrength, opponent.T)
    poisonMod = rules.PoisonedAttacks ? -1 : 0  // -1 to needed = +1 modifier

    wounds = poisonAutoWounds
    killingBlows = 0
    normalWounds = 0
    for i in 1..(hits - poisonAutoWounds):
        roll = rng.D6()
        needed = max(2, toWoundNeeded + poisonMod)  // 2+ minimum (nat 1 always fails)

        if opponent.SpecialRules.Flammable and rules.FlamingAttacks:
            if roll < needed:
                roll = rng.D6()  // re-roll failed wounds vs Flammable

        wounded = (roll >= needed)

        if wounded:
            kbThreshold = rules.KillingBlowDualSource ? 5 : 6
            isKB = rules.KillingBlow and (roll >= kbThreshold)
            isHKB = rules.HeroicKillingBlow and (roll >= kbThreshold)

            if (isKB or isHKB) and not autoWound:
                eligible = IsKBEligible(opponent.TroopType, isHKB)
                if eligible:
                    killingBlows += 1
                else:
                    // Non-eligible: 1 wound ignoring armour+regen
                    normalWounds += 1  // flagged as ignoresArmour+Regen
            else:
                normalWounds += 1

    // --- Armour Saves ---
    armourSave = opponent.EffectiveArmourSave(effectiveStrength, rules.ArmourPiercing ?? 0)
    unsavedNormal = 0
    for i in 1..normalWounds:
        if rules.IgnoresArmourSaves or rules.LightningAttacks:
            // Lightning: still saved by Natural Armour
            if rules.LightningAttacks:
                natArmourSave = opponent.NaturalArmourOnly(effectiveStrength)
                if natArmourSave != null and rng.D6() >= natArmourSave: continue
            unsavedNormal += 1
        elif armourSave != null and armourSave <= 6:
            if rng.D6() >= armourSave:
                continue  // saved
            unsavedNormal += 1
        else:
            unsavedNormal += 1

    // --- Ward Saves / Regeneration ---
    // Determine best ward save (ward vs regen, take best)
    wardValue = opponent.SpecialRules.WardSave
    regenValue = opponent.SpecialRules.Regeneration
    parryValue = opponent.SpecialRules.Parry  // front arc only

    // Parry restrictions
    if parryValue != null:
        if effectiveStrength >= opponent.S + 3: parryValue = null  // too strong
        // mounted models can't parry (unless specified)

    totalWardSave = BestOf(wardValue, regenValue, parryValue)

    // Killing Blow wounds: only Ward Save applies (not Regen, not Parry)
    kbWardedOff = 0
    for i in 1..killingBlows:
        if wardValue != null and rng.D6() >= wardValue:
            kbWardedOff += 1

    // Normal wounds: full ward/regen/parry
    // But: if Flaming, regen is negated
    effectiveWard = totalWardSave
    if rules.FlamingAttacks and regenValue != null:
        effectiveWard = BestOf(wardValue, parryValue)  // regen excluded

    normalWardedOff = 0
    for i in 1..unsavedNormal:
        if effectiveWard != null and rng.D6() >= effectiveWard:
            normalWardedOff += 1

    // Ethereal: special ward saves
    if opponent.SpecialRules.Ethereal:
        etherealWard = rules.MagicalAttacks ? 5 : 2  // 5+ vs magic, 2+ vs mundane
        // Apply ethereal ward instead of normal ward (take best)
        // ... (handled in ward calculation above via ParsedSpecialRules)

    // --- Multiple Wounds ---
    finalWounds = (killingBlows - kbWardedOff)  // KB wounds = model's remaining W
    unsavedAfterWard = unsavedNormal - normalWardedOff
    if rules.MultipleWounds != null:
        for i in 1..unsavedAfterWard:
            mw = RollMultipleWounds(rules, rng)
            finalWounds += min(mw, opponent.PrimaryProfile.W)  // can't exceed model's wounds
    else:
        finalWounds += unsavedAfterWard

    return AttackResult(hits, wounds, killingBlows, kbWardedOff + normalWardedOff, finalWounds)
```

## Algorithm: `ComputeCombatRes()`

```pseudo
function ComputeCombatRes(unit, woundsDealt, context, isAttacker):
    cr = woundsDealt

    // Rank bonus
    cr += unit.RankBonus  // 0-3, based on complete ranks behind first

    // Standard bearer
    if unit.HasStandardBearer: cr += 1

    // Charge bonus
    isCharging = (isAttacker and context.charge == AttackerCharged)
                 or (not isAttacker and context.charge == DefenderCharged)
    if isCharging: cr += 1

    // Fear bonus (from core-rules-new.json Fear description)
    if unit.HasFearCausingModels:
        fearUS = unit.FearCausingUnitStrength()
        enemyUS = opponent.TotalUnitStrength()
        if fearUS >= 2 * enemyUS:
            cr += 2
        else:
            cr += 1
        // But: if opponent majority causes Fear, they ignore this bonus

    return CombatResBreakdown(wounds=woundsDealt, rank=rankBonus, standard=..., charge=..., fear=...)
```

## Algorithm: `ResolveImpactHits()`

```pseudo
function ResolveImpactHits(charger, target, rng):
    impactHits = charger.TotalImpactHits(rng)  // from troop type + special rules, highest source
    if impactHits == 0: return 0

    // Impact Hits use model's Strength (mount's S for cavalry troop type IH)
    strength = charger.ImpactHitStrength()

    // Roll to wound, armour saves, ward saves (no Parry)
    // Model's special rules do NOT apply (no Poison, KB, etc.)
    wounds = 0
    for i in 1..impactHits:
        toWoundNeeded = CoreRules.GetToWoundScore(strength, target.T)
        if rng.D6() >= toWoundNeeded:
            armourSave = target.EffectiveArmourSave(strength, 0)  // no AP
            if armourSave == null or rng.D6() < armourSave:
                // Ward save (no parry on impact hits)
                ward = target.BestWardSave(excludeParry=true)
                if ward == null or rng.D6() < ward:
                    wounds += 1
    return wounds
```

## Algorithm: Strike Order

```pseudo
function SortByStrikeOrder(attackGroups):
    // Each group has: unit, isASF, isASL, initiative
    // Priority: Impact Hits (already handled) > ASF > Initiative > ASL > Stomp (already handled)

    for each group:
        if group.hasASF and group.hasASL:
            // Cancel out, use normal initiative
            group.priority = group.initiative
        elif group.hasASF:
            group.priority = 100 + group.initiative  // strikes first tier
        elif group.hasASL:
            group.priority = -100 + group.initiative  // strikes last tier
        else:
            group.priority = group.initiative

    sort groups by priority descending
    // Groups with same priority strike simultaneously
```

## Test Cases

### Table Lookup Tests
- `GetToHitScore(3, 3)` → 4 (WS3 vs WS3)
- `GetToHitScore(1, 10)` → 6 (WS1 vs WS10)
- `GetToHitScore(10, 1)` → 2 (WS10 vs WS1)
- `GetToWoundScore(4, 4)` → 4 (S4 vs T4)
- `GetToWoundScore(3, 7)` → 6 (S3 vs T7)
- `GetArmourSaveModifier(3)` → 0
- `GetArmourSaveModifier(5)` → -2

### Armour Save Tests
- Heavy armour (3+) + shield (+1) vs S3 (no mod) → 2+ save
- Heavy armour (3+) vs S5 (-2) → 5+ save
- Heavy armour (3+) + Natural Armour (5+) [+2] vs S4 (-1) → 3+ base, -1 = 4+ save? [VERIFY calc]
- No armour vs S3 → no save

### Special Rule Tests (deterministic with seeded RNG)
- **Poisoned Attacks**: 10 wounds at S3 vs T4 (need 5+). With poison (+1 modifier) → need 4+. Verify ~50% wound rate over large sample.
- **Killing Blow**: 100 wounds rolled, verify ~16.7% trigger rate. Against Infantry: model slain. Against Monster: 1 wound ignoring armour+regen.
- **Regeneration (4+)**: 100 unsaved wounds, verify ~50% saved. With Flaming Attacks: 0% saved from regen (ward still applies).
- **Hatred**: First round: all misses re-rolled. Second round: no re-rolls (unless dual-source).
- **Frenzy**: Verify +1 Attack applied. After losing combat: Frenzy lost (Iteration 3).
- **ASF vs ASF**: Both units have ASF, Initiative 5 vs 3. Higher I strikes first, gets re-roll. Lower I strikes second, no re-roll.
- **ASF + ASL**: Model has both. Verify both cancel, normal Initiative order, no re-rolls.
- **Impact Hits**: Cavalry charges (Impact Hits 1 per model), verify hits resolved at mount's Strength, no Parry allowed.

### Combat Resolution Tests
- 5 wounds + rank bonus 3 + standard 1 = 9 CR
- Unit with Fear (+1 CR) vs unit without → +1
- Charging unit → +1 CR

### Integration Tests
- **Saurus Warriors (20, 5-wide) vs Empire Swordsmen (20, 5-wide)**: Known stat lines, verify plausible win/loss percentages.
- **Ethereal unit vs mundane unit**: Attacker should almost never wound (2+ ward). Vs magical attacks: 5+ ward.
- **KB unit vs multi-wound model**: Verify KB triggers score remaining wounds for CR.
