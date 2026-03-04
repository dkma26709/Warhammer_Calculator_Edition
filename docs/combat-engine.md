# Combat Engine

## Architecture

```
CombatEngine.Simulate()          # Runs 10,000 iterations
  └── CombatResolver.ResolveRound()   # Single round, 5 phases
        ├── AttackResolver.ResolveImpactHits()   # Phase 1
        ├── AttackResolver.ResolveAttacks()      # Phases 2-3 (per attack group)
        ├── CombatResolver.ResolveStomp()        # Phase 4
        └── CombatResolver.ComputeCombatRes()    # Phase 5
```

## Combat Phases

### Phase 1: Impact Hits (charge only)

Triggered when `ChargeState` is `AttackerCharged` or `DefenderCharged`.

- **Cavalry / Monstrous Cavalry:** 1 impact hit per model in the front rank (from troop type)
- **Special rules:** `ImpactHits(N)` or `ImpactHitsRoll(DX)` per front-rank model
- Uses the higher of troop-type impact hits and special-rule impact hits (don't stack)
- Wounds at model's Strength (mount S for cavalry) + Mighty Blow in round 1
- Saves: armour + ward/regen (no parry)

### Phase 2-3: Melee Attacks (strike order)

Four attack groups are built and sorted by priority:

| Priority | Condition |
|----------|-----------|
| 100 + I | Always Strikes First (cancelled if also ASL) |
| I | Normal initiative order |
| -100 + I | Always Strikes Last (cancelled if also ASF) |

**Groups:** Attacker rider, Attacker mount, Defender rider, Defender mount.

Each group calls `AttackResolver.ResolveAttacks()`.

### Phase 4: Stomps (Always Strikes Last)

- **Eligible stompers:** Monstrous Infantry, Monstrous Beasts, Monstrous Cavalry, Monsters
- **Eligible targets:** Infantry, Large Infantry, War Beasts, Swarms
- **Hits:** Monster = D6, others = 1 (overridden by Stomp special rule)
- Wounds at model's Strength + Mighty Blow (round 1)

### Phase 5: Combat Resolution

Wounds are capped at the target unit's total wound pool (`ModelCount * W`) before CR calculation.

| Component | Source |
|-----------|--------|
| Wounds | Total unsaved wounds dealt |
| Rank Bonus | min(ranks - 1, 3), requires minimum frontage |
| Standard Bearer | +1 if present |
| Charge Bonus | +1 if charging this round |
| Fear Bonus | +1 if unit causes Fear/Terror and opponent doesn't; +2 if unit strength >= 2x opponent |

Winner = higher CR total. Ties are draws.

---

## Attack Resolution Detail (`AttackResolver.ResolveAttacks`)

### 1. Attack Count

- **Rider:** `UnitConfiguration.TotalAttacks()` — base A + Frenzy (+1) + Devastating Charge (+1 on charge) + weapon extra attacks. Front rank at full attacks, supporting ranks capped by troop type.
- **Mount:** `UnitConfiguration.MountAttacks()` — split-profile cavalry/monstrous cavalry only. Support attacks: Cavalry 1/model, Monstrous Cavalry 2/model.

### 2. Effective Strength

- Base from profile or weapon (`"As user"`, `"+1"`, or fixed value)
- Mighty Blow (round 1 only): highest of unit MB and weapon MB (don't stack)
- Weapon MB can be conditional (only when charged to front, e.g. Spear)

### 3. To-Hit Roll

- Lookup from 10x10 WS vs WS table
- Natural 1 always misses, natural 6 always hits
- **Re-rolls:**
  - Hatred: re-roll misses (first round only)
  - ASF: re-roll misses (if attacker I > defender I)
  - ASL penalty: re-roll hits (if attacker I < defender I)

### 4. To-Wound Roll

- Lookup from 10x10 S vs T table
- Poisoned Attacks: -1 to wound needed (min 2+)
- Flaming Attacks: re-roll failed wounds vs Flammable targets
- Natural 1 always fails, natural 6 always wounds

### 5. Killing Blow / Heroic Killing Blow

On natural 6 to wound:
- **KB eligible targets:** Infantry, Large Infantry, Cavalry, War Beasts
- **HKB eligible targets:** Everything
- KB kills one model outright (all remaining wounds on that model)
- Against non-eligible targets: 1 wound ignoring armour and regen instead
- Ward saves still apply to KB wounds (but not regen or parry)

### 6. Armour Saves

- Base from equipment armour + Natural Armour + troop type intrinsic save
- Modified by attacker's Strength and Armour Piercing
- Minimum useful save: 2+
- **Bypassed by:** Ignores Armour Saves, KB wounds
- **Lightning Attacks:** ignore all armour except Natural Armour

### 7. Ward Saves

Best of Ward Save / Regeneration / Parry applies (one roll):
- Flaming Attacks negate Regeneration
- KB wounds: only Ward Save applies (no regen, no parry)
- Parry doesn't work if attacker S >= defender S + 3
- Ethereal: 5+ ward vs magical attacks, 2+ vs mundane

### 8. Wound Allocation (model-by-model tracking)

Wounds are allocated sequentially to models in the defending unit:

- **KB/HKB:** Kills the current model outright (consumes remaining HP). Next wound starts on a fresh model.
- **Multiple Wounds:** Rolled per unsaved wound, capped at the current model's remaining HP. Excess is lost — does not carry over to the next model.
- **Normal wounds (1 each):** Allocated one at a time. When a model reaches 0 HP, the next model starts at full HP.
- **Total cap:** Cannot exceed `ModelCount * W` for the entire unit.

---

## Monte Carlo Output

`CombatSimulationResult` contains:

| Field | Description |
|-------|-------------|
| `AttackerWinPct` / `DefenderWinPct` / `DrawPct` | Win/loss/draw percentages |
| `AvgAttackerWounds` / `AvgDefenderWounds` | Average wounds dealt per round |
| `AvgAttackerCombatRes` / `AvgDefenderCombatRes` | Average total CR |
| `AvgAttackerBreakdown` / `AvgDefenderBreakdown` | Average CR by component |
| `Distribution` | CR difference histogram |
| `AttackerWoundDistribution` / `DefenderWoundDistribution` | Wound count histograms |
