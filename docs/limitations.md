# Known Limitations

## Currently Implemented

### Combat Mechanics
- Single combat round resolution (Monte Carlo, 10k iterations)
- Full to-hit / to-wound / armour save / ward save pipeline
- 32 special rules with correct interactions
- Killing Blow and Heroic Killing Blow with eligibility checks
- Multiple Wounds (fixed and dice-based) with model-by-model allocation
- Impact Hits (cavalry charge + special rules)
- Stomps (monstrous units vs eligible targets)
- Strike order: ASF / ASL / Initiative priority
- Re-rolls: Hatred (round 1), ASF (if I > opponent), Flaming vs Flammable
- Cavalry split profiles and mount attacks
- Weapon modifiers: Initiative, extra attacks, Mighty Blow, Fight in Extra Ranks, Parry
- Effective armour save: equipment + Natural Armour + intrinsic troop type save
- Ward Save / Regeneration / Parry precedence
- Ethereal (5+ ward vs magical, 2+ vs mundane)
- Combat Resolution: wounds + rank bonus + standard + charge + fear
- Wound pool capping: can't deal more wounds than models * W

### UI
- Two-unit matchup with full configuration
- Weapon selection from equipment + options
- Command group checkboxes (conditional on unit data)
- Points cost tracking with per-model and flat upgrades
- Effective stats comparison panel
- Win/loss/draw distribution charts
- Points exchange efficiency analysis

### Data
- 15 faction rosters extracted from PDFs
- Core rules tables, weapons, armour, common special rules
- Army-specific extras (magic items, lores, army rules) — loaded but not used in combat

## Not Yet Implemented

### Combat Mechanics
- **Multi-round combat** — only round 1 is simulated. Mighty Blow and Hatred are round-1-only effects; subsequent rounds would drop these bonuses.
- **Break tests / Steadfast** — no Leadership rolls after losing combat
- **Fleeing and pursuit** — no retreat/overrun simulation
- **Flank and rear charges** — attacks always assumed front-to-front
- **Characters joining units** — no character allocation, "Look Out, Sir!", or challenge mechanics
- **Magic phase** — no spellcasting, bound spells, or dispelling
- **Shooting phase** — close combat only
- **Random Attacks** — parsed but not rolled (units with Random Attacks use their profile A instead)
- **Berserk Rage / Frenzy loss** — Frenzy gives +1A but doesn't model post-combat restraint tests
- **Fear/Terror psychology tests** — Fear CR bonus is modelled, but actual Leadership tests to charge or hold are not
- **Magic Resistance** — not implemented
- **Stubborn / Unbreakable / Unstable** — parsed but not used (no break tests to apply them to)

### Data / Extraction
- **Faction-specific special rules** — Predatory Fighter, Jungle Poisons, etc. are extracted as text but not parsed into combat effects
- **Faction-specific weapons** — weapon dropdown only matches the core rules weapon registry; army-unique weapons aren't resolved
- **Mount stat line extraction** — recently fixed for units with trailing `-` in points column; other armies should be re-extracted to pick up the fix
- **Magic items** — extracted but not usable in combat configuration

### UI
- No army list builder — single unit vs single unit only
- No save/load for unit configurations
- No comparison mode (e.g. "what if I add shields?")
- No mobile-optimised layout
