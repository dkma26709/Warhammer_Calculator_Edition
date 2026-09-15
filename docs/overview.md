# Warhammer Fantasy Combat Simulator

## What It Is

A Blazor WebAssembly (.NET 10) application that simulates Warhammer Armies Revamped (WAR) close
combat matchups using Monte Carlo dice simulation, and builds army lists against the edition's
composition rules. Select two units, configure them, and run 10,000 simulated combats to see
win/loss probabilities, average wounds, and combat resolution breakdowns.

Four pages:

- **Matchup** (`/`, `/matchup`) — two units head-to-head, single or multi-round simulation
- **Gauntlet** (`/gauntlet`) — profile one unit against a list of opponent matchups
- **Builder** (`/builder`) — army list builder with composition validation, magic items, banners,
  wizard lores, faction upgrade families, JSON export/import, and LocalStorage autosave
- **Roster** (`/roster`) — printable battle sheet for the current list

## Tech Stack

- **Framework:** Blazor WASM (.NET 10)
- **Styling:** Bootstrap 5 (CDN)
- **Charts:** plain HTML/CSS bars (no chart library)
- **Data:** one JSON bundle per game system, exported from the `warhammer-armies-revamped` repo
- **Tests:** xUnit, about 600 test methods across 50 test classes

## Data Pipeline

```
Warhammer Armies Project army books (PDF)
        |
  Re-typeset as Typst in warhammer-armies-revamped, revised into the WAR edition
        |
  python export.py --check      (that repo's gate: every record, byte-identical text)
        |
  build/war.json  →  copied to wwwroot/data/war/war.json on a release
        |
  BundleService (fetch once, factions on demand) + custom converters
        |
  Domain models + Combat engine
```

Beside the bundle, `core-rules.json` and `troop-types.json` are hand-maintained per system for
the combat engine: the rulebook's charts and the Troop Types chapter's figures, which the
bundle's prose rulebook cannot feed.

### Supported Factions (30)

Albion, Amazons, Araby, Beastmen, Bretonnia, Chaos Dwarfs, Daemons of Chaos, Dark Elves, Dogs of
War, Dwarfs, The Empire, Estalia, Grand Cathay, Halflings, High Elves, Hobgoblins, Kingdoms of
Ind, Kislev, Lizardmen, Nippon, Norsca, Ogre Kingdoms, Orcs & Goblins, Pirates of Sartosa,
Skaven, Tomb Kings, Vampire Counts, Warriors of Chaos, Wood Elves, Zombie Pirates

Earlier editions (WAP 9th v2 and v3) are not shipped by this build; use an older build for those.

## How To Run

```bash
cd Warhammer
dotnet run
```

Navigate to `http://localhost:5075/`.

## How To Test

```bash
cd Warhammer.Tests
dotnet test
```

## Documentation Index

- [Project Structure](structure.md) — files, folders, and their roles
- [Combat Engine](combat-engine.md) — how combat simulation works
- [Data Models](data-models.md) — the bundle format and C# model mapping
- [Components & UI](components.md) — Blazor component hierarchy and behaviour
- [Known Limitations](limitations.md) — what's not yet implemented
