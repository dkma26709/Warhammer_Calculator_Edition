# Components & UI

## Page Layout

`Matchup.razor` (routes: `/`, `/matchup`) is the sole page. Layout:

```
┌─────────────────┬──────────────┬─────────────────┐
│   Attacker      │   Controls   │   Defender       │
│   UnitPanel     │  Charge +    │   UnitPanel      │
│   (col-md-5)    │  Simulate    │   (col-md-5)     │
│                 │  (col-md-2)  │                  │
├─────────────────┴──────────────┴─────────────────┤
│              EffectiveStatsPanel                   │
│         (shown when both units selected)           │
├──────────────────────────────────────────────────┤
│                 ResultsPanel                       │
│          (shown after simulation runs)             │
└──────────────────────────────────────────────────┘
```

## Component Hierarchy

```
Matchup.razor
├── UnitPanel (attacker)
│   ├── UnitSelector        — faction/category/unit dropdowns
│   └── UnitConfigurator    — stats, config, command group
│       ├── StatTable        — M/WS/BS/S/T/W/I/A/Ld row
│       └── SpecialRuleBadges — colour-coded rule tags
├── CombatControls          — charge dropdown + simulate button
├── UnitPanel (defender)     — same structure
├── EffectiveStatsPanel     — computed matchup stats
└── ResultsPanel            — results, charts, breakdowns
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

### EffectiveStatsPanel

**Parameters:** `Attacker`, `Defender`, `ChargeState`, `CoreRules`

Side-by-side comparison showing pre-combat computed stats:

| Stat | Description |
|------|-------------|
| Base Contact | Models in geometric contact |
| Total Attacks | Including mount, Frenzy, weapon bonuses |
| Effective Strength | With weapon + Mighty Blow |
| Effective Initiative | With weapon modifier |
| To Hit | Needed roll (WS vs WS table) |
| To Wound | Needed roll (S vs T table, with Poison) |
| Armour Save | Vs opponent's strength + AP |
| Ward Save | Best of ward/regen/parry |
| Impact Hits | If charging (count shown) |

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
