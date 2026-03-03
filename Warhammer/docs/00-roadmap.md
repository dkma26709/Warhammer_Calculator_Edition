# WAP Combat Simulator — Project Roadmap

## Project Goal

A Blazor WASM application that simulates Warhammer Armies Project (WAP) combat matchups using Monte Carlo dice simulations. Users select two units, configure them, and see probabilistic combat outcomes.

**Critical constraint**: All game rules and data come exclusively from the extracted WAP PDFs. No rules from external sources, LLM knowledge, or non-WAP editions. The `wap-rules.md` file contains user-verified rule interpretations that override extraction text where ambiguous.

## Technology Stack

- Blazor WebAssembly (.NET 10)
- Bootstrap (CDN) for UI
- xUnit for unit tests
- JSON data files extracted from WAP PDFs
- ApexCharts (or similar) for result visualizations

## Data Foundation

Already complete — not part of the rewrite:

| Extractor | Output | Content |
|-----------|--------|---------|
| `extract_pdf.py` | 15x `{faction}.json` | 920 units across 15 armies |
| `extract_core_rules.py` | `core-rules-new.json` | Combat tables, 22+ weapons, 6 armour, 50+ common special rules, troop types |
| `extract_army_extras.py` | 15x `{faction}-extras.json` | 130 army rules, 1092 magic items, 29 lores (356 spells), 222 faction upgrades |

## Iteration Plan

### Iteration 1: Core Combat Simulation

**Goal**: Accurate single-round close combat simulation with all WAP special rules.

**Scope**:
- Unit selection: faction → unit → profile
- Unit configuration: model count, frontage, weapon choice (from core rules weapons), armour, command group
- Stat display: profile stats, effective stats (after equipment/rules), special rule badges
- Combat simulation: Monte Carlo (N=10,000), to-hit, to-wound, armour saves, ward saves
- All combat-affecting common special rules implemented correctly per WAP
- Combat resolution: wounds + rank bonus + standard bearer + charge bonus
- Results display: win/loss/draw %, average wounds, combat resolution breakdown
- Distribution charts for combat outcomes

**Special rules to implement** (combat-affecting, from core-rules-new.json):
- Always Strikes First / Last
- Armour Piercing (X)
- Devastating Charge
- Fight in Extra Ranks (X)
- Flaming Attacks
- Frenzy
- Hatred
- Heroic Killing Blow
- Ice/Lightning/Magical Attacks
- Impact Hits (X)
- Killing Blow
- Mighty Blow (X)
- Multiple Wounds (X)
- Natural Armour (X)
- Parry
- Poisoned Attacks
- Regeneration (X+)
- Ward Save (X+)

**Success criteria**:
- Correct to-hit/to-wound lookups matching PDF tables
- All listed special rules affect combat outcomes correctly per WAP rules
- Armour save calculation matches WAP (strength modifiers, natural armour stacking, chaos armour)
- Base contact and rank bonus calculations correct per troop type
- Monte Carlo results converge (low variance across runs)
- Unit tests pass for all combat logic

### Iteration 2: Magic Items (Common + Faction)

**Goal**: Equip units with magic items that modify combat — both common items (available to all armies) and faction-specific items.

**Pre-requisite**: Extract common magic items from core rules PDF (add to `extract_core_rules.py` or separate script). These are not yet in `core-rules-new.json`.

**Scope**:
- Extract and add common magic items to core rules data
- Magic weapon selection (replaces/augments base weapon)
- Magic armour selection (replaces/augments base armour)
- Talisman selection (ward saves, other effects)
- Enchanted items (misc effects)
- Magic standards (unit-wide effects)
- Item restrictions enforcement (points limits, "one per army", character-only)
- Parse item effect text into stat modifications
- Common items available to all factions; faction items only to their faction

**Success criteria**:
- Common magic items extracted from core rules PDF and added to data
- All common + faction magic items available for eligible units
- Item effects correctly modify combat stats
- Item restrictions enforced

### Iteration 3: Multi-Round Combat

**Goal**: Simulate extended combats with break tests, pursuit, and casualty tracking.

**Scope**:
- Break test simulation (Leadership - combat res difference)
- Steadfast rule (Ld on unmodified value if more ranks)
- Pursuit and flee distances
- Casualty removal between rounds (from rear)
- Rank recalculation after casualties
- Multi-round results display (round-by-round breakdown)

**Success criteria**:
- Break test outcomes match expected probabilities
- Casualty tracking correctly reduces unit effectiveness
- Steadfast triggers at correct times

### Iteration 4: Extended Calculations

**Goal**: Probability calculators beyond melee combat.

**Scope**:
- Spell casting probabilities (cast value vs power dice)
- Leadership test probabilities (Ld value, modifiers, Cold-Blooded)
- Charge range probabilities (2D6 + M, swiftstride)
- Potentially: shooting phase simulation

**Success criteria**:
- Probability calculations match mathematical expectations
- UI clearly presents probability distributions

## Out of Scope

These will NOT be built:
- Army list builder / points calculator
- Full game simulation (movement, magic phase, shooting phase as integrated game)
- Multiplayer / online play
- Army book browsing / reference tool
- PDF viewing or extraction (already done)
- Mobile app (responsive web is sufficient)
