# Known Limitations

> Verified against the source on 2026-09-15. Every "not implemented" entry below was checked by
> grepping for its mechanism, not inherited from the previous revision — the file had drifted badly
> enough to list five shipped features as missing, so please re-check rather than re-copy.

## Currently Implemented

### Combat Mechanics
- Monte Carlo resolution (default 10k iterations), single round and **multi-round**
  (`CombatEngine.SimulateMultiRound`, `MultiRoundResult`)
- Full to-hit / to-wound / armour save / ward save pipeline
- **70 typed special-rule properties** on `ParsedSpecialRules`, with defined interactions
- **Break tests, Steadfast, Stubborn, Unbreakable and Unstable** (`BreakTestResolver`)
- **Fleeing and pursuit**, including Swift Stride catch rates (`FleeAndPursueResolver`)
- Killing Blow and Heroic Killing Blow with eligibility checks
- Multiple Wounds (fixed and dice-based) with model-by-model allocation
- Impact Hits (cavalry charge + special rules) and Stomps vs eligible targets
- **Random Attacks are rolled**, not approximated — `DiceRoller.Roll(rules.RandomAttacksRoll)`
- Strike order: ASF / ASL / Initiative priority
- Re-rolls: Hatred (round 1), ASF (if I > opponent), Flaming vs Flammable
- Cavalry split profiles and mount attacks; rules the book scopes to one model go to that model
- Weapon modifiers: Initiative, extra attacks, Mighty Blow, Fight in Extra Ranks, Parry
- Mighty Blow and Armour Piercing are cumulative across sources, per both rulebooks
- Ward saves combine (capped at 4+); Regeneration, Parry and Dodge take best-of
- Effective armour save: equipment + Natural Armour + intrinsic troop type save
- Ethereal (5+ ward vs magical, 2+ vs mundane)
- Combat Resolution: wounds + rank bonus + standard + charge + fear + musician
- Wound pool capping: can't deal more wounds than models × W
- Some faction rules are parsed into effects (e.g. Predatory Fighter)

### UI
- Four pages: `/matchup` (two units), `/gauntlet` (one unit vs many — the comparison mode),
  `/builder` (army lists), `/roster` (printable battle sheet)
- **Army list builder** with the bundle's composition rules (percentages, the duplicate chart, each book's own unit constraints), magic items, banners, wizard lores and faction upgrade families
- **Printable roster** — profiles, saves, equipment, items, rules and points, browser Save-as-PDF
- **JSON export / import and LocalStorage autosave** (`ArmyListSerializer`, `ArmyListStorage`)
- Weapon selection from equipment + options; command group checkboxes from unit data
- Points cost tracking with per-model and flat upgrades
- Effective stats comparison panel; win/loss/draw distributions; points efficiency analysis

### Data
- **One game system** — Warhammer Armies Revamped, 30 army books from one bundle
- Core rules tables, weapons, armour and troop types beside it, hand-maintained
- Profile variants (a Captain or a General from one entry) and character mounts from the book's mounts section
- Magic items and banners are **selectable and affect combat** through `UnitModifier`

## Not Yet Implemented

### Combat Mechanics
- **Flank and rear charges** — attacks always assumed front-to-front
- **Characters joining units** — no character allocation, "Look Out, Sir!", or challenges
- **Magic phase** — no spellcasting, bound spells or dispelling. Lore and spells are selectable in
  the builder and recorded on the list, but nothing casts them
- **Shooting phase** — close combat only. Missile weapons are parsed and printed on the roster
  sheet but resolve to nothing in the engine
- **Fear / Terror psychology tests** — the Fear combat-resolution bonus is modelled; the Leadership
  tests to charge or to hold are not
- **Frenzy restraint** — Frenzy gives +1A but post-combat restraint tests are not modelled
- **Magic Resistance** — named by many units, no typed property, no effect
- **~25 further named rules have no typed property**, so they reach the badges and the roster sheet
  but the engine cannot act on them. The ones the data names most often: Strider (68/74 units),
  Loner (40/9), Aquatic (20/21), Stupidity (14/14), Expendable (11/10), Inspiring Presence (2/10),
  plus Hidden, Sniper, Weapon Team, Animated Construct and Mixed Unit. Most are movement, shooting
  or deployment rules with no close-combat effect, which is why they were never prioritised —
  Stupidity, Inspiring Presence and Loner are the ones that touch Leadership and could matter now
  that break tests exist. The abandoned `seperate_weapons` branch enumerates all of them.
- **Third and fourth profiles are never fielded**, so a chariot fights with its crew's attacks and
  not its beasts', and the chassis makes a phantom attack
- **Champion-only rules go nowhere** — the engine fields one profile for the rank and file

### Data
- **Faction-specific weapons** — the weapon dropdown matches the core rules registry only;
  army-unique weapons aren't resolved to a profile
- **Magic item effects are not derived from their description text** — each selection carries a
  `UnitModifier` the user fills in by hand
- **Upstream bundle gaps** — the rulebook's common items carry no ids (the loader derives one from
  the name), 26 units' special-rules strings are cut off mid-parenthesis (the parser drops the
  dangling fragment), two entries share an id with another (`albion/amanthas`,
  `high-elves/great-eagle`), and 25 option lines are unclassified and render as plain toggles
- **Handler ratios inside a unit** ("one Handler per 5 War Hounds") are not validated
- `crew` / `drawnBy` (per-profile model counts, as prose) are read by nothing; `magic` is only
  shown as text

### UI
- No mobile-optimised layout
- Validation is advisory — no code path gates export on issue severity
