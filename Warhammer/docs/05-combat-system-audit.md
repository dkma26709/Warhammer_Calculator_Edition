# Combat System — Design Audit Document

This document traces exactly how a combat round is resolved, step by step, mapping each WAP rule to its implementation. Its purpose is to make bugs visible by laying bare the logic chain from user configuration through to final wound output.

---

## Table of Contents

1. [Unit Configuration](#1-unit-configuration)
2. [Combat Round Overview](#2-combat-round-overview)
3. [Phase 1: Impact Hits](#3-phase-1-impact-hits)
4. [Phase 2–3: Melee Attacks (Strike Order)](#4-phase-23-melee-attacks-strike-order)
5. [Phase 4: Stomps](#5-phase-4-stomps)
6. [Phase 5: Combat Resolution](#6-phase-5-combat-resolution)
7. [Special Rules Reference](#7-special-rules-reference)
8. [Known Bugs & Issues](#8-known-bugs--issues)

---

## 1. Unit Configuration

**File**: `Models/Domain/UnitConfiguration.cs`

When a unit is selected, a `UnitConfiguration` is built from its `UnitEntry` JSON data:

| Property | Source | Logic |
|---|---|---|
| `PrimaryProfile` | First profile with a `points` value, else `profiles[0]` | `ResolveProfiles()` |
| `MountProfile` | Second profile (if exists) | `ResolveProfiles()` |
| `TroopType` | `unit.TroopType` string | `TroopTypeInfo.Parse()` — extracts base type, Character flag, intrinsic armour |
| `BaseWidthMm` | `unit.BaseSize` (e.g. `"25x25"`) | Regex extracts first number, default 25 |
| `SpecialRules` | `unit.SpecialRules` text | `ParsedSpecialRules.Parse()` — comma-split, pattern-match 30+ rules |
| `EquipmentWeapons` | `unit.Equipment` text | `EquipmentParser.Parse()` — normalized to core-rules.json keys |
| `EquipmentArmour` | `unit.Equipment` text | Same parser, armour keys |
| `ModelCount` | User-set (default: min unit size) | Character defaults to 1 |
| `Frontage` | User-set (default: `TroopType.MinRankWidth`) | Character defaults to 1 |
| `SelectedWeaponKey` | User-set (default: first equipment weapon) | Key into `core-rules.json` weapons |

### Derived Values

| Value | Formula | Notes |
|---|---|---|
| `Ranks` | `ModelCount / Frontage` (integer division) | |
| `RankBonus` | `min(Ranks - 1, 3)` | Only if `Frontage >= TroopType.MinRankWidth` |
| `ModelsInFrontRank` | `min(Frontage, ModelCount)` | |

### Troop Type Properties

**Data, per system** — `wwwroot/data/{system}/troop-types.json`, resolved by
`GameDataRepository.ResolveTroopTypesAsync()` into `TroopTypeRules` and handed to
`TroopTypeInfo.Parse(troopType, rules)`. `TroopTypeInfo` holds no defaults of its own, so a
missing row throws on load instead of quietly serving another edition's figures.

wap-v2 and wap-v3 disagree on supporting attacks for the monstrous types, on mount supporting
attacks (wap-v3 allows none), on Cavalry/Monstrous Cavalry Impact Hits (wap-v3 grants none) and
on Swiftstride (no wap-v3 troop type grants it). See `docs/03-combat-rules.md` for the tables.

---

## 2. Combat Round Overview

**Entry point**: `CombatEngine.Simulate()` runs N iterations (default 10,000). Each iteration calls `CombatResolver.ResolveRound()`.

**File**: `Calculator/CombatResolver.cs`

A single round proceeds through five phases:

```
Phase 1: Impact Hits (charge turn only)
Phase 2-3: Melee Attacks (sorted by strike order)
Phase 4: Stomps
Phase 5: Combat Resolution
```

After all phases, total wounds dealt to each side are **capped** at the target's wound pool:
```
cap = defender.ModelCount * defender.PrimaryProfile.W
```

---

## 3. Phase 1: Impact Hits

**File**: `Calculator/AttackResolver.cs` — `ResolveImpactHits()`

**Trigger**: Only on the charge turn. Determined by `CombatContext.Charge`.

### Who gets impact hits

Impact hits come from two sources (highest wins, they don't stack):

| Source | Count |
|---|---|
| Unit special rule `ImpactHits(X)` or `ImpactHitsRoll` | Value * ModelsInFrontRank |
| Troop type default (Cavalry, Monstrous Cavalry) | 1 * ModelsInFrontRank |

### Resolution per hit

```
1. Determine Strength:
   - Use mount's S if mount profile exists, else model's S
   - Add Mighty Blow if combat round 1

2. Roll to Wound:
   - Look up S vs T in toWound table

3. Armour Save:
   - defender.EffectiveArmourSave(strength, AP=0, strengthMod)
   - No Armour Piercing from impact hits

4. Ward Save:
   - Best of WardSave and Regeneration
   - Ethereal: override to 2+ (no magical attack check)
   - No Parry against impact hits
```

---

## 4. Phase 2–3: Melee Attacks (Strike Order)

### 4a. Building Attack Groups

**File**: `Calculator/CombatResolver.cs` — `BuildAttackGroups()`

Each side generates up to two attack groups:

| Group | Profile Used | Weapon | Notes |
|---|---|---|---|
| Rider attacks | `PrimaryProfile` | Selected weapon | Weapon rules parsed |
| Mount attacks | `MountProfile` | None (natural weapons) | Only if `TroopType.HasSplitProfile` |

### 4b. Strike Order (Priority)

Each group gets a priority score:

```
If ASF and ASL: Priority = Initiative  (cancel out)
If ASF only:    Priority = 100 + Initiative  (strikes first)
If ASL only:    Priority = -100 + Initiative  (strikes last)
Otherwise:      Priority = Initiative
```

Groups sorted descending by priority. Higher initiative within the same band strikes first.

**Mount ASF/ASL**: Mounts are always assigned `HasASF = false, HasASL = false`. Only rider's ASF/ASL affects the rider group's priority.

### 4c. Counting Attacks

**File**: `Models/Domain/UnitConfiguration.cs` — `TotalAttacks()`

For rider attacks:
```
baseAttacks = PrimaryProfile.A
  + (Frenzy ? 1 : 0)
  + (isCharging && DevastatingCharge ? 1 : 0)
  + weapon.ExtraAttacks

frontRankAttacks = ModelsInFrontRank * baseAttacks

supportingRanks = Ranks - 1
maxSupportRanks = 1 + max(unit.FightInExtraRanks, weapon.FightInExtraRanks)
actualSupportRanks = min(supportingRanks, maxSupportRanks)

supportAttacksPerModel = min(baseAttacks, TroopType.MaxSupportingAttacks)
supportAttacks = actualSupportRanks * Frontage * supportAttacksPerModel

totalAttacks = frontRankAttacks + supportAttacks
```

For mount attacks (`MountAttacks()`):
```
frontAttacks = ModelsInFrontRank * MountProfile.A
supportPerModel = Cavalry:1, MonstrousCavalry:2, else:0
supportAttacks = min(Ranks-1, 1) * Frontage * supportPerModel
total = frontAttacks + supportAttacks
```

**Note**: Mount attacks do NOT get Frenzy or Devastating Charge bonuses.

### 4d. Effective Strength

**File**: `Models/Domain/UnitConfiguration.cs` — `EffectiveStrength()`

```
baseS = PrimaryProfile.S
weaponS = ParseWeaponStrength(weapon.Strength, baseS)
  -> "As user" = baseS, "+1" = baseS+1, "3" = 3

Mighty Blow (first round only):
  unitMB = SpecialRules.MightyBlow ?? 0
  weaponMB = (determined by AttackResolver, see below)
  effectiveMB = max(unitMB, weaponMB)   <- highest wins, don't stack
  if combatRound == 1: S += effectiveMB

Mount attacks: use mount's S directly, no weapon modifiers.
```

#### Weapon Mighty Blow conditional logic

**File**: `Calculator/AttackResolver.cs`, lines 76-93

```
weaponMB = weaponRules.MightyBlow (e.g., Spear: 1, Great Weapon: 1, Flail: 2)

applyWeaponMB = weaponRules.MightyBlowCondition switch:
    WhenCharging     => isCharging       (lances)
    WhenBeingCharged => isBeingCharged   (spears/pikes)
    Always           => true             (flails, great weapons)

if MightyBlowExcludesLightTargets && target is Infantry/LargeInfantry/Swarms/Shrine:
    applyWeaponMB = false              (spear/pike restriction)
```

Condition is determined from weapon notes: "the model charges" → WhenCharging (lances), "charged to its front" → WhenBeingCharged (spears/pikes). Asterisk on the rule text triggers condition lookup.

### 4e. Rolling to Hit

**File**: `Calculator/AttackResolver.cs`, lines 96-131

```
toHitNeeded = closeCombatToHit[attackerWS][defenderWS]
  (defenderWS uses max of rider/mount WS for split-profile defenders)

Re-rolls (use effective Initiative = profile.I + weapon mods):
  Hatred:  re-roll failed hits (first round only)
  ASF:     re-roll failed hits IF attacker has ASF, no ASL,
           AND effectiveAttackerI > effectiveDefenderI
  ASL:     re-roll SUCCESSFUL hits IF attacker has ASL, no ASF,
           AND effectiveAttackerI < effectiveDefenderI

Roll: hit on 6 always, miss on 1 always, otherwise roll >= toHitNeeded
```

### 4f. Rolling to Wound

```
defenderT = max(rider.T, mount.T) for split-profile, else rider.T
toWoundNeeded = toWound[effectiveStrength][defenderT]

Poison: if PoisonedAttacks, toWoundNeeded reduced by 1 (min 2)
Flaming vs Flammable: re-roll failed wound rolls

Roll to wound, then check for Killing Blow:
```

### 4g. Killing Blow / Heroic Killing Blow

```
On natural 6 to wound (5+ if dual-source KB, not implemented):
  KB eligible targets:  Infantry (all subtypes), Cavalry, War Beasts
  HKB eligible targets: everything EXCEPT Swarms (treated as regular KB for Swarms)

  If eligible: model slain outright (counted separately as killingBlows)
  If not eligible: 1 wound, ignores armour saves and regeneration (separate ward save path)
```

### 4h. Armour Saves

```
Non-eligible KB wounds: skip armour entirely (handled in separate ward save path)

For each normal wound:
  If attacker has IgnoresArmourSaves:
    Skip armour save entirely (but Lightning Attacks still allow Natural Armour)

  If attacker has LightningAttacks:
    Only Natural Armour saves apply (with strength modifier)

  Otherwise:
    armourSave = defender.EffectiveArmourSave(strength, AP, strengthMod)
    Roll >= armourSave to save
```

#### Armour Save Calculation

**File**: `Models/Domain/UnitConfiguration.cs` — `EffectiveArmourSave()`

```
totalModifier = 0
For each armour piece in EquipmentArmour:
    totalModifier += (modifier from core-rules.json combatSave field)
    e.g., lightArmour "+1/6+" -> modifier = 1

totalModifier += (7 - NaturalArmour value)      if present
totalModifier += (7 - IntrinsicArmourSave value) if present (e.g., Chariot)

baseSave = 7 - totalModifier
penalty = (-strengthModifier) + attackerAP
modifiedSave = baseSave + penalty
return max(modifiedSave, 2)
```

Where `strengthModifier` comes from `core-rules.json`:
```
S1-S3: 0, S4: -1, S5: -2, S6: -3, S7: -4, ...
```

### 4i. Ward Saves / Regeneration / Parry

```
wardValue     = defender.SpecialRules.WardSave ?? 7
regenValue    = defender.SpecialRules.Regeneration ?? 7
parryValue    = defender.SpecialRules.Parry ?? 7

Ethereal override:
  if defender.Ethereal:
    etherealWard = (attacker.MagicalAttacks ? 7 : 2)
    wardValue = min(wardValue, etherealWard)

Parry restriction:
  if attacker.effectiveStrength >= defender.S + 3 OR defender is mounted (HasSplitProfile):
    parryValue = 7  (disabled)

Flaming negates regeneration:
  if attacker.FlamingAttacks: regenValue effectively ignored

Three ward save categories:
  KB wounds:           wardValue only (no regen, no parry)
  Non-eligible KB:     min(wardValue, parryValue) — no regen (KB always negates regen)
  Normal wounds:       min(wardValue, regenValue, parryValue)
                       (with flaming: min(wardValue, parryValue) only)
  Roll >= effectiveWard to save
```

### 4j. Wound Allocation (Model-by-Model)

```
Track: modelsRemaining, currentModelHP (starts at defender.W)

KB wounds first:
  Each KB kills the current model outright:
    finalWounds += currentModelHP
    modelsRemaining--
    currentModelHP = defenderW (next model)

Non-eligible KB wounds:
  1 wound each, no Multiple Wounds, no carry over
  Allocated same as normal 1-damage wounds

Normal wounds:
  For each unsaved wound:
    damagePerHit = MultipleWounds roll or fixed value (default 1)
    actualDamage = min(damagePerHit, currentModelHP)  <- capped per model
    finalWounds += actualDamage
    currentModelHP -= actualDamage
    if currentModelHP <= 0: next model

Excess MW damage is LOST (no carry-over between models).
```

---

## 5. Phase 4: Stomps

**File**: `Calculator/CombatResolver.cs` — `ResolveStomp()`

### Eligibility

| Stomper must be | Target must be |
|---|---|
| Monstrous Infantry, Monstrous Beasts, Monstrous Cavalry, Monster | Infantry, Large Infantry, War Beasts, Swarms |

### Hit Count

| Source | Hits |
|---|---|
| Special rule `Stomp(X)` | X |
| Special rule `StompRoll` (e.g., "D3") | Roll |
| Type default: Monster | D6 (Thunderstomp) |
| Type default: other eligible | 1 |

### Resolution per hit

```
strength = stomper.PrimaryProfile.S
  + MightyBlow (if round 1)

toWound via S vs T table
Armour save (AP=0)
Ward save:
  Flaming stomps negate Regeneration
  Ethereal targets get ward vs non-magical stomps
  No Parry on stomps
  ward = min(WardSave, Regeneration) [adjusted for Flaming/Ethereal]
```

---

## 6. Phase 5: Combat Resolution

**File**: `Calculator/CombatResolver.cs` — `ComputeCombatRes()`

```
Combat Resolution = Wounds + RankBonus + StandardBearer + ChargeBonus + FearBonus

  Wounds:         total wounds dealt to the opponent (after all caps)
  RankBonus:      unit.RankBonus (max +3, requires min rank width)
  StandardBearer: +1 if unit has standard bearer
  ChargeBonus:    +1 if this unit charged this turn
  FearBonus:      +1 if unit causes Fear/Terror and opponent doesn't
                  +2 if additionally unit's US >= 2x opponent's US

Post-CR: Musician draw bonus
  If CR is a draw and one side has a musician but the other doesn't:
    that side wins by 1 (MusicianBonus = 1)
  If both sides have a musician: still a draw
```

The side with higher CR wins the round. Difference = attacker CR - defender CR.

---

## 7. Special Rules Reference

### Combat-Affecting Rules — Implementation Status

| Rule | Parsed | Used in Combat | Notes |
|---|---|---|---|
| Always Strikes First | Yes | Yes | Strike order priority +100; re-roll misses if I > defender |
| Always Strikes Last | Yes | Yes | Strike order priority -100; re-roll hits if I < defender |
| Armour Piercing (X) | Yes | Yes | Applied in armour save formula |
| Chaos Armour | Yes | Yes | +1 armour save with light/medium/heavy armour |
| Cold-Blooded | Yes | No | Not combat-relevant (Leadership) |
| Devastating Charge | Yes | Yes | +1 Attack when charging |
| Ethereal | Yes | Yes | Ward save override |
| Fight in Extra Ranks | Yes | Yes | Extra supporting ranks (best of unit/weapon) |
| Flaming Attacks | Yes | Yes | Negates Regen on melee, impact hits, stomps; re-roll wounds vs Flammable |
| Flammable | Yes | Yes | Grants attacker wound re-rolls |
| Fear | Yes | Yes | CR bonus |
| Frenzy | Yes | Yes | +1 Attack (always) |
| Hatred | Yes | Yes | Re-roll failed hits (round 1) |
| Heroic Killing Blow | Yes | Yes | On natural 6 to wound, works vs all types except Swarms (regular KB) |
| Ice Attacks | Yes | No | Parsed but no special combat effect implemented |
| Ignores Armour Saves | Yes | Yes | Skip armour save entirely |
| Impact Hits | Yes | Yes | Charge-only phase |
| Killing Blow | Yes | Yes | On natural 6 to wound, eligible targets only |
| Lightning Attacks | Yes | Yes | Ignore armour except Natural Armour |
| Magical Attacks | Yes | Yes | Used for Ethereal check |
| Mighty Blow (X) | Yes | Yes | +X Strength in round 1 |
| Multiple Wounds (X/DX) | Yes | Yes | Capped per model, no carry-over |
| Natural Armour (X+) | Yes | Yes | Stacks with equipment armour |
| Parry (X+) | Yes | Yes | Unit rules + weapon Parry + shield/buckler Parry; disabled by S+3 or mounted |
| Poisoned Attacks | Yes | Yes | +1 to wound modifier |
| Random Attacks | Yes | Yes | Rolled per combat, overrides profile.A |
| Regeneration (X+) | Yes | Yes | Ward save, negated by Flaming/KB |
| Stomp | Yes | Yes | Post-melee phase |
| Stubborn | Yes | No | Not combat-relevant (Break test) |
| Terror | Yes | Yes | Implies Fear for CR bonus |
| Unbreakable | Yes | No | Not combat-relevant (Break test) |
| Unstable | Yes | No | Not combat-relevant (Break test) |
| Ward Save (X+) | Yes | Yes | After-armour save |

### Weapon Rules — Implementation Status

| Rule | Parsed | Used | Notes |
|---|---|---|---|
| Initiative Mod (+/-N) | Yes | Yes | Applied in BuildAttackGroups |
| Extra Attacks (+N) | Yes | Yes | Added to base attacks |
| Mighty Blow (X) | Yes | Yes | Highest of weapon/unit MB |
| Fight in Extra Ranks | Yes | Yes | Best of weapon/unit FiER |
| Parry (X+) | Yes | Yes | Weapon Parry checked; shield/buckler Parry requires hand weapon |
| Requires Two Hands | Yes | **No** | Parsed but no equipment restriction enforced |
| ChargedToFrontInitiativeBonus | Yes | Yes | Spear/Pike: +1 extra I when being charged |
| InitiativeModCondition | Yes | Yes | `WeaponCondition` enum: Always / WhenCharging / WhenBeingCharged |
| MightyBlowCondition | Yes | Yes | Same enum; lances = WhenCharging, spears = WhenBeingCharged |
| MightyBlowExcludesLightTargets | Yes | Yes | Spear/Pike: MB skips Infantry, Large Infantry, Swarms, Shrines |

---

## 8. Bugs Found & Resolution Log

All bugs identified during both audit rounds have been fixed. This section is preserved as a record of what was found and how it was resolved. Round 2 bugs are in Section 9.

### BUG 1 -- Armour Piercing sign was inverted [FIXED]

**Severity**: High
**Root cause**: `modifiedSave = baseSave - strengthModifier - attackerAP` — AP (positive int) was subtracted, improving the save instead of worsening it.
**Fix**: Refactored to `penalty = (-strengthModifier) + attackerAP; modifiedSave = baseSave + penalty;`

---

### BUG 2 -- IsCharging was broken for the defender side [FIXED]

**Severity**: High
**Root cause**: `IsCharging()` always returned `context.Charge == AttackerCharged` regardless of which unit was attacking.
**Fix**: Removed the helper. Added `isAttackerSide` parameter to `ResolveAttacks`. Charge state is now resolved inline using both `ChargeState` and `isAttackerSide`.

---

### BUG 3 -- Lance Mighty Blow / Initiative conditions were inverted [FIXED]

**Severity**: High
**Root cause**: A single `MightyBlowOnlyWhenCharged` bool treated spears (when being charged) and lances (when charging) identically, and the condition checked "being charged" for both. Lance Initiative asterisk was also stripped during parsing.
**Fix**: Introduced `WeaponCondition` enum (`Always`, `WhenCharging`, `WhenBeingCharged`). Both `MightyBlowCondition` and `InitiativeModCondition` are now determined from weapon notes: "the model charges" = `WhenCharging` (lances), "charged to its front" = `WhenBeingCharged` (spears/pikes). Added `MightyBlowExcludesLightTargets` for spear/pike target restriction (BUG 6).

---

### BUG 4 -- Chaos Armour was not implemented [FIXED]

**Severity**: Medium
**Root cause**: "Chaos Armour" fell through to the default case in `ParsedSpecialRules.Parse()`.
**Fix**: Added `ChaosArmour` bool to `ParsedSpecialRules`, parser case, and +1 armour modifier in `EffectiveArmourSave` when unit has light/medium/heavy armour.

---

### BUG 5 -- Ethereal ward against magical attacks [FIXED]

**Severity**: Medium
**Root cause**: `etherealWard = magical ? 5 : 2` gave ethereal units a free 5+ ward vs magical attacks.
**Fix**: Changed to `magical ? 7 : 2` — no special ward against magical attacks.

---

### BUG 6 -- Spear/Pike Mighty Blow target restriction was missing [FIXED]

**Severity**: Medium
**Root cause**: Spear/Pike notes state "Mighty Blow does not apply towards Infantry, Large Infantry, Swarms and Shrines" but no check existed.
**Fix**: Added `MightyBlowExcludesLightTargets` flag to `ParsedWeaponRules`, parsed from weapon notes. `AttackResolver` skips weapon MB when target is Infantry, Large Infantry, Swarms, or Shrines.

---

### BUG 7 -- Random Attacks were not used in combat [FIXED]

**Severity**: Medium
**Root cause**: `RandomAttacks`/`RandomAttacksRoll` were parsed but never read in attack resolution.
**Fix**: `ResolveAttacks` now rolls random attacks and passes the result as `attacksOverride` to `TotalAttacks`.

---

### BUG 8 -- Weapon and shield Parry was not applied [FIXED]

**Severity**: Low-Medium
**Root cause**: Only unit-level `SpecialRules.Parry` was used. Weapon Parry (Two Hand Weapons) and armour Parry (shields/bucklers) were ignored.
**Fix**: `AttackResolver` now checks the defender's weapon for Parry via `ParsedWeaponRules`, and checks shield/buckler armour entries for Parry (conditional on hand weapon being selected).

---

### BUG 9 -- Impact hits vs Ethereal ignored magical attacks [FIXED]

**Severity**: Low
**Root cause**: Ethereal 2+ ward was applied to all impact hits regardless of charger's Magical Attacks.
**Fix**: `ResolveImpactHits` now checks `charger.SpecialRules.MagicalAttacks` and sets ethereal ward to 7 (none) if magical.

---

### VERIFY -- Monstrous Cavalry default impact hits [CONFIRMED CORRECT]

WAP core rules state: "Monstrous Cavalry are subject to the following special rules: Fear, Impact Hits (1), Swiftstride." Code correctly gives 1 impact hit per front-rank model.

---

### VERIFY -- Large Infantry MinRankWidth [CONFIRMED CORRECT]

WAP core rules state: "A unit of Large Infantry is required to be four or more models wide in order to have a complete rank." Code correctly uses MinRankWidth = 4.

---

## 9. Round 2 Bugs — Found & Fixed

### BUG 12 -- HKB worked against Swarms (should be treated as regular KB) [FIXED]

**Severity**: Low
**Root cause**: `IsKBEligible()` returned `true` for all HKB targets including Swarms.
**Fix**: Added Swarms exception: `if (isHeroic && targetType.BaseType != BaseTroopType.Swarms) return true;`. HKB vs Swarms falls through to regular KB eligibility check, which excludes Swarms → non-eligible KB path.

---

### BUG 13 -- Non-eligible KB wounds did not skip Regeneration [FIXED]

**Severity**: Medium
**Root cause**: Non-eligible KB wounds were mixed into the normal wound pool with only an armour-skip flag. They still faced Regeneration in the ward save phase, but WAP states they should ignore both armour AND regen.
**Fix**: Separated `nonEligibleKBWounds` into its own category. These wounds skip armour entirely and use a ward save path that excludes Regeneration (ward + parry only, no regen).

---

### BUG 14 -- Impact hits didn't apply Flaming Attacks vs Regeneration [FIXED]

**Severity**: Medium
**Root cause**: `ResolveImpactHits` computed ward as `Math.Min(wardValue, regenValue)` without checking if the charger had Flaming Attacks.
**Fix**: Added `flamingImpact` check — if charger has Flaming Attacks, regen is excluded from the ward save.

---

### BUG 15 -- Stomps didn't apply Flaming Attacks vs Regeneration [FIXED]

**Severity**: Medium
**Root cause**: Same as BUG 14 but for `ResolveStomp`. Ward was `Math.Min(ward, regen)` with no flaming check.
**Fix**: Added `flamingStomp` check — if stomper has Flaming Attacks, regen is excluded.

---

### BUG 16 -- Stomps didn't handle Ethereal targets [FIXED]

**Severity**: Low
**Root cause**: Impact hits checked for Ethereal ward, but stomps did not. Non-magical stomps against Ethereal targets would deal full damage.
**Fix**: Added Ethereal ward check to `ResolveStomp`: non-magical stomps get a 2+ ward from Ethereal.

---

### VERIFY-4 -- ASF/ASL Initiative comparison used raw profile I [FIXED]

**Root cause**: ASF re-roll and ASL re-roll penalty compared raw `profile.I` values, ignoring weapon Initiative modifiers (e.g., great weapon -2I, spear +1I).
**Fix**: Both attacker and defender effective Initiative are now computed with weapon mods before the ASF/ASL comparison.

---

### FEATURE -- Musician draw bonus [ADDED]

**Description**: If combat resolution is a draw and one side has a musician while the other doesn't, the side with a musician wins by 1.
**Implementation**: Added to `CombatResolver.ResolveRound` after CR computation. `MusicianBonus` field added to `CombatResBreakdown`, aggregated in `CombatEngine`, displayed in `ResultsPanel`.
