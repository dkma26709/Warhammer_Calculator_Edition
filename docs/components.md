# Components & UI

## Page Layout

`Matchup.razor` (routes: `/`, `/matchup`), laid out as the War Room redesign
(`Warhammer/docs/07-war-room-redesign.md`); the Gauntlet page follows the same shape
with the subject on the left and the opponent roster on the right. Layout at 1200px and up
(it stacks below that):

```
┌─────────────────┬──────────────────┬─────────────────┐
│ UnitHero        │ CombatControls   │ UnitHero        │  hero band
│ (attacker)      │ + WinSplit       │ (defender)      │
├─────────────────┼──────────────────┼─────────────────┤
│ LoadoutPanel    │ ResultsPanel /   │ LoadoutPanel    │
│ (attacker)      │ MultiRoundResults│ (defender)      │
└─────────────────┴──────────────────┴─────────────────┘
```

## Component Hierarchy

```
Matchup.razor
├── UnitHero (attacker)     — portrait, stat line, "This fight" line (EffectiveStatsCalculator), rule chips
├── CombatControls          — charge buttons, rounds, iterations, Simulate
├── WinSplit                — headline win/draw (or prevail/neither) split
├── UnitHero (defender)     — mirrored
├── LoadoutPanel (attacker)
│   ├── UnitSelector        — faction/category/unit dropdowns
│   └── UnitConfigurator    — config, command group (stat table and rules shown in the hero)
├── ResultsPanel            — wound distributions, CR margin, CR and points tables
│   or MultiRoundResultsPanel
└── LoadoutPanel (defender) — same structure
```

## Components

### UnitSelector

**Parameters:** `SelectedFactionKey`, `OnFactionSelected`, `FactionUnits`, `SelectedUnitName`, `OnUnitSelected`

Three cascading dropdowns:
1. **Faction** — 15 armies
2. **Category filter** — All / Lords / Heroes / Core / Special / Rare / Named Characters
3. **Unit** — sorted by category then name

Handles duplicate unit names by appending equipment + points + troop type as a disambiguator.

### UnitConfigurator

**Parameters:** `Unit`, `Configuration`, `ConfigurationChanged`, `CoreRules`

Displays and edits:
- **Stat table** (primary + mount profiles)
- **Points display** (per-model cost and total)
- **Model count & frontage** inputs (non-characters only)
- **Weapon dropdown** — populated from equipment + options (only shown if >1 choice)
- **Command group checkboxes** — Champion, Musician, Standard Bearer (only shown if unit has the option available)
- **Equipment summary** — first line of equipment text
- **Special rule badges**

### CombatControls

**Parameters:** `ChargeState`, `ChargeStateChanged`, `CanSimulate`, `IsSimulating`, `OnSimulate`

- Charge dropdown: Neither / Attacker Charged / Defender Charged
- Simulate button (disabled until both units selected, shows spinner during simulation)

### UnitHero

**Parameters:** `Label`, `Side`, `Unit`, `Config`, `FactionKey`, `Stats`, `OpponentStats`

One side of the Matchup hero band: portrait, name, stat line and special rules, plus a
"This fight" line once both units are chosen. The line is `EffectiveStats.FightSummary`, from
`EffectiveStatsCalculator`, so it is tested C# rather than Razor:

| Part | Description |
|------|-------------|
| Base contact | Models in geometric contact |
| Attacks at S | Including mount, Frenzy, weapon bonuses; Strength with weapon + Mighty Blow |
| Hits on | Needed roll (WS vs WS table) |
| Wounds on | Needed roll (S vs T table), "(poison)" when poisoned; "cannot wound" when the chart says n/a |
| Impact hits | If charging (count shown) |
| Armour / ward | Vs opponent's strength + AP; best of ward/regen/parry; left out when there is none |
| I, strike order | Initiative with weapon modifier; strike order left out when either side has a mount |

### ResultsPanel

**Parameters:** `Result`, `AttackerName`, `DefenderName`, `AttackerConfig`, `DefenderConfig`

Sections:
1. **Headline stats** — average wounds dealt and win percentage per side
2. **Win/Draw/Loss bar** — stacked percentage visualisation
3. **Wound distribution charts** — bar histograms for each side
4. **Combat resolution breakdown** — table of wounds + rank + standard + charge + fear
5. **Points exchange** — investment, average points destroyed, efficiency ratio; identifies points winner vs CR winner
6. **CR distribution chart** — histogram of combat resolution differences

### StatTable

**Parameters:** `Profile`, `MountProfile`

Simple table: M / WS / BS / S / T / W / I / A / Ld. Shows `*` for null values. Two rows if mount profile present.

### SpecialRuleBadges

**Parameters:** `Rules`

Colour-coded badges:
- **Red:** Offensive rules (KB, Hatred, Frenzy, Poison, Flaming, MW, AP, etc.)
- **Blue:** Defensive rules (Ward, Regen, Natural Armour, Parry, Ethereal)
- **Yellow:** Morale rules (Fear, Terror, Stubborn, Unbreakable, Unstable, Cold-Blooded)
- **Grey:** Everything else
