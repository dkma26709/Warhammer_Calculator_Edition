# Warhammer Fantasy Combat Simulator

## What It Is

A Blazor WebAssembly (.NET 10) application that simulates Warhammer Armies Project (WAP) 9th Edition close combat matchups using Monte Carlo dice simulation. Select two units, configure them, and run 10,000 simulated combats to see win/loss probabilities, average wounds, and combat resolution breakdowns.

Three pages:

- **Matchup** (`/`, `/matchup`) — two units head-to-head, single or multi-round simulation
- **Gauntlet** (`/gauntlet`) — profile one unit against a list of opponent matchups
- **Builder** (`/builder`) — army list builder with WAP composition validation, magic items, banners, wizard lores, JSON export/import, and LocalStorage autosave

## Tech Stack

- **Framework:** Blazor WASM (.NET 10)
- **Styling:** Bootstrap 5 (CDN)
- **Charts:** plain HTML/CSS bars (no chart library; ApexCharts was planned but never adopted)
- **Data:** JSON files extracted from WAP army book PDFs
- **Tests:** xUnit, ~290 test methods across ~25 test classes

## Data Pipeline

```
WAP Army Book PDFs (source of truth)
        |
  Python extractors (pdfplumber + regex)
        |
  JSON files (F:\Warhammer\...\Extracted\json\)
        |
  Copied to wwwroot/data/factions/
        |
  C# deserialisation (HttpClient + custom converters)
        |
  Domain models + Combat engine
```

### Extractors (at `F:\Warhammer\Warhammer pdfs\WAP\Extracted\`)

| Script | Output | Description |
|--------|--------|-------------|
| `extract_pdf.py` | `{faction}.json` | Unit profiles, stats, equipment, options, special rules |
| `extract_core_rules.py` | `core-rules.json` | To-hit/wound tables, weapons, armour, common special rules |
| `extract_army_extras.py` | `{faction}-extras.json` | Army rules, magic items, lores of magic, faction upgrades |

### Supported Factions (15)

Beastmen, Bretonnia, Daemons of Chaos, Dark Elves, Dwarfs, High Elves, Lizardmen, Ogre Kingdoms, Orcs & Goblins, Skaven, The Empire, Tomb Kings, Vampire Counts, Warriors of Chaos, Wood Elves

## How To Run

```bash
cd Warhammer
dotnet run
```

Navigate to `https://localhost:{port}/matchup` (or just `/`).

## How To Test

```bash
cd Warhammer.Tests
dotnet test
```

## Documentation Index

- [Project Structure](structure.md) — files, folders, and their roles
- [Combat Engine](combat-engine.md) — how combat simulation works
- [Data Models](data-models.md) — JSON schemas and C# model mapping
- [Components & UI](components.md) — Blazor component hierarchy and behaviour
- [Known Limitations](limitations.md) — what's not yet implemented
