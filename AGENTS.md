# AGENTS.md — Warhammer Calculator Edition

Reference for AI agents (and humans) working in this repo. Verified against the source on 2026-09-15.

## What This Is

A **Blazor WebAssembly (.NET 10)** app that simulates **Warhammer Armies Revamped (WAR)** tabletop combat using Monte Carlo dice simulation (default 10,000 iterations), plus a full army list builder. All game data lives in static JSON files under `wwwroot/data/`; there is no backend server.

WAR is a new edition built on the Warhammer Armies Project army books, typeset in the sibling repo `warhammer-armies-revamped`, which exports the whole edition as one bundle file for this app. Earlier editions (wap-v2, wap-v3) are no longer shipped by this build; players of those editions use an older build.

Four pages:

| Route | File | Purpose |
|---|---|---|
| `/` and `/matchup` | `Warhammer/Pages/Matchup.razor` | Two units head-to-head: win %, wounds, combat-resolution breakdown, distributions |
| `/gauntlet` | `Warhammer/Pages/Gauntlet.razor` | One "subject" unit profiled against a list of opponent matchups |
| `/builder` | `Warhammer/Pages/Builder.razor` | Army list builder with composition validation from the bundle, magic items, banners, wizard lores, faction upgrade families, JSON export/import, LocalStorage autosave |
| `/roster` | `Warhammer/Pages/Roster.razor` | Printable battle roster for the current list — stats, saves, equipment, rules and points under Characters/Core/Special/Rare headings. Prints via the browser's own Save-as-PDF |

## Build / Run / Test

```bash
# Run the app (from repo root)
cd Warhammer && dotnet run          # then open http://localhost:5075/

# Run tests (about 600 [Fact]/[Theory] methods over 50 files; ~1,060 cases once theories expand)
cd Warhammer.Tests && dotnet test

# Build everything
dotnet build Warhammer/Warhammer.sln
```

- Tests are **xUnit** (`[Fact]`/`[Theory]`).
- Test project: `Warhammer.Tests/`, references the main project directly. `TestHelpers.cs` has shared builders and option-group fixtures.
- CI: `.github/workflows/test.yml` builds and tests on pushes to `main` and PRs.
- `CombatEngine.Simulate`/`SimulateMultiRound` accept an optional `seed` parameter for reproducible runs.

## Repo Layout

```
Warhammer/                  # Main Blazor WASM project (also contains Warhammer.sln)
├── Program.cs              # Entry point + DI registration
├── Pages/                  # Matchup, Gauntlet, Builder, Roster
├── Components/             # Reusable Razor components. UnitConfigurator is a thin shell
│                           #   composing UnitOptionsEditor, WizardConfigPanel,
│                           #   MagicBannerSelector, CommandGroupPanel, FactionAbilityPanel,
│                           #   CustomModifierList, MagicItemSelector, ModifierEditor —
│                           #   all sharing the Configuration/ConfigurationChanged pattern
├── Layout/MainLayout.razor
├── Calculator/             # Combat simulation engine (pure C#, no UI deps)
│   ├── CombatEngine.cs         # Monte Carlo orchestrator; entry point Simulate(...)
│   ├── CombatResolver.cs       # Single-round resolution; PrepareAttackGroups + ResolveRound
│   ├── AttackResolver.cs       # To-hit → to-wound → saves; special rule handling (biggest file)
│   ├── BreakTestResolver.cs    # Break tests (Ld, steadfast, stubborn, ...)
│   ├── FleeAndPursueResolver.cs
│   ├── BaseContactCalculator.cs # Geometric models-in-contact math
│   ├── DiceRoller.cs
│   └── Models/             # CombatContext, CombatResults, CombatUnitState,
│                           #   MultiRoundResult, GauntletMatchup, FactionRaces
├── Models/
│   ├── Json/               # DTOs mirroring the JSON exactly (records; no logic):
│   │                       #   BundleData (rulebook, composition, factions), FactionData,
│   │                       #   UnitEntry, OptionGroup/OptionChoice, CoreRulesData,
│   │                       #   TroopTypesData, and the lenient converters
│   ├── Domain/             # Business models: Unit, UnitConfiguration (+ ArmyLoadout for
│   │                       #   builder-only state), OptionSelections, CompositionRules,
│   │                       #   FactionUpgrades, ParsedSpecialRules, TroopTypeInfo, ...
│   └── Enums/              # Strongly-typed keys: OptionKind, SelectMode, CommandRole,
│                           #   UnitCategory, CloseCombatWeaponKey, ArmourKey — each with
│                           #   *Extensions mappers
├── Services/               # Data loading + army builder services (see below)
├── docs/                   # DESIGN/SPEC docs (00-roadmap ... 06-army-builder)
└── wwwroot/
    ├── data/{system}/                  # One folder per game system (currently only war)
    │   ├── {system}.json               # THE BUNDLE: every army book, the rulebook's rules,
    │   │                               #   magic items and lores, and the composition rules.
    │   │                               #   formatVersion 2, exported by export.py in
    │   │                               #   warhammer-armies-revamped and copied here on a
    │   │                               #   release (its `source` is the commit). Generated,
    │   │                               #   never hand-edited; WarBundleTests guards the copy.
    │   ├── core-rules.json             # Hand-maintained: to-hit/to-wound charts, weapons,
    │   │                               #   armour, armour save modifiers — the engine's tables
    │   └── troop-types.json            # Hand-maintained: the Troop Types chapter's figures
    ├── data/portrait-manifest.json     # Generated by tools/PortraitFetcher, keyed by faction id
    ├── images/units/{faction-id}/{slug}.png  # Unit portraits (fetched from TW wiki)
    └── js/army-builder.js              # JS interop: file download + localStorage

Warhammer.Tests/            # xUnit tests (flat folder, one class per subject)
docs/                       # DESCRIPTIVE docs of the implementation (overview, structure,
                            #   combat-engine, data-models, components, limitations)
tools/PortraitFetcher/      # Console app: fetches unit portraits from the Total War
                            #   Warhammer fandom wiki API, writes images + manifest
```

## Architecture

Data flows **upward**: bundle JSON → Services (load/cache) → Domain models → Calculator (simulate) → Components → Pages. User input flows back down as `UnitConfiguration` / `ArmyList` objects.

### DI registration (`Warhammer/Program.cs`)

| Service | Lifetime | Role |
|---|---|---|
| `GameSystemService` | Scoped | Holds the active game system key (from localStorage at startup); all data URLs go through its `DataPath()`. Switching systems persists the key and forces a full page reload — no cache invalidation anywhere |
| `CoreRulesService` | Scoped | Loads/caches `core-rules.json`; to-hit/to-wound/armour table lookups |
| `BundleService` | Scoped | Fetches `{system}.json` once, lists its factions off their header fields, and deserialises one faction at a time on first use. Also loads `troop-types.json` |
| `IGameDataRepository` → `GameDataRepository` | Scoped | **Preferred facade** — composes the two services above, resolves raw JSON into domain `Weapon`/`Armour`/`Unit` objects, caches per faction, builds `CompositionRules` per faction. Pages inject this, not the lower services |
| `CombatEngine` | Transient | Monte Carlo simulation |
| `ArmyListValidator` | Singleton | Composition rules (percent caps, the duplicate chart, each book's own unit constraints, General/BSB, magic item and upgrade family budgets) |
| `ArmyListSerializer` | Scoped | ArmyList ↔ JSON DTO (export/import/autosave), schema `army-list/v2`, addressed by the bundle's ids |
| `ArmyRosterBuilder` | Scoped | ArmyList → `RosterDocument`, the flattened battle sheet the `/roster` print view renders |
| `PortraitService` | Scoped | Resolves portrait URLs against `portrait-manifest.json`; graceful fallback if missing |

Pages call `Repo.EnsureLoadedAsync()` (and `Portraits.EnsureLoadedAsync()`) before use; all loads are idempotent and cached.

### Combat simulation flow

1. `CombatEngine.Simulate(attacker, defender, context, iterations)` — builds attack groups **once** via `CombatResolver.PrepareAttackGroups` (weapon parsing, parry, sort by Initiative), then loops `ResolveRound` N times aggregating wins/wounds/CR breakdowns/distributions.
2. `CombatResolver.ResolveRound` — one full combat round: impact hits, attacks in initiative order, combat resolution (wounds + rank bonus + standard + charge + fear + musician).
3. `AttackResolver` — one side's attacks: to-hit (WS table, rerolls from Hatred etc.), to-wound, armour/ward/regeneration saves, Killing Blow, Poisoned, Multiple Wounds, etc. Tier-3 roll modifiers from `UnitModifier` are wired in here.
4. Multi-round: `MultiRoundResult` + `BreakTestResolver` + `FleeAndPursueResolver` handle break tests and flee/pursue between rounds.

The Calculator layer is **stateless and UI-free** — it takes configured units and a `Random`, returns records. Keep it that way; this is what makes it testable.

## The bundle, in short

`wwwroot/data/war/war.json` is formatVersion 2 of the format documented in `warhammer-armies-revamped/docs/format.md`. What the app reads:

- **Top level**: `formatVersion`, `system`, `version`, `source` (git sha), `rulebook` (`rules` by name, `items`, `lores` with spells, `weapons` as text), `composition`, `factions` keyed by slug.
- **Composition**: `pointsCategories` (characters/core/special/rare, plus lords/heroes for a book that splits them), `singleUnitMaxPercent`, `general`, `battleStandard`, `specialCharacters`, `duplicates` (bands per points bracket plus a growth rule), `sources`. A faction's own `composition.unitConstraints` are appended when the Builder loads that faction (`GetCompositionRulesAsync(factionId)`).
- **Faction**: `id`, `name`, `align`, `rules`, `items`, `upgrades` (the book's upgrade chapters — Knightly Orders, Blessed Spawnings, Runic Items — each entry with `chapter`, `group`, `cost` flat or tiered), `lores`, `units`.
- **Unit**: `id`, `name`, `section` + `tier` + `named` (→ `UnitCategory`), `profiles` with ids, `unitSizeParsed`, `troopType`, `baseSize`, `equipment`, `specialRules`, `magic`, `notes`, and `optionGroups`.
- **Option group**: `id`, `kind` (`OptionKind`), `select` (`SelectMode`: one/any/count), `min`/`max`, `appliesTo` (a profile id), `raw`, and per-kind fields: `role` (command), `pointsLimit`/`itemTypes`/`families` (magicItems), `family` (factionUpgrade), `rule`, `points`. Choices carry `id`, `name`, `points`, `perModel`, `replaces`, `unitId` (a mount's own entry), nested `options` that a taken choice unlocks, and `min`/`max`/`everyModels` for counted picks.

## Conventions & Gotchas

- **Json vs Domain model split is strict.** `Models/Json/` records mirror JSON exactly and contain no logic. `Models/Domain/` wraps them with parsing/computed properties. Don't add `[JsonPropertyName]` business logic hybrids. The option groups are the one JSON shape the domain holds directly (`Unit.OptionGroups`), with all reading done through the `OptionGroupExtensions` helpers.
- **Factions are ids, not an enum.** A faction is the bundle's slug (`"empire"`, `"orcs-goblins"`) everywhere: `Unit.FactionId`, `ArmyList.FactionId`, saved lists, portrait folders and `Calculator/Models/FactionRaces` (the race keywords Hatred and Fear Elves read). Adding a book means nothing in code except a `FactionRaces` row, and `BundleDataTests` fails until it has one.
- **Ids are the handles.** Units, profiles, option groups, choices, items and upgrade entries carry the bundle's stable ids; `OptionSelections` is keyed by group id and choice id, `ArmyListDto` v2 stores those ids, and a saved list survives a renamed unit (the serializer falls back to the name with a warning). Two known export defects give an id to two entries (`albion/amanthas`, `high-elves/great-eagle`); the repository keeps the first, and `BundleDataTests` lists them so a new one is loud.
- **Lenient converters.** The bundle writes a few fields in more than one shape: `specialRules` and `upgrades` as a string, a list, or a list of `{name, text}` definitions; `notes` as strings or objects; an item's `bound` as null, `true` or an object; a profile's `points` as a number or `"-"`. `Models/Json/Converters.cs` reads each into one C# shape. When parsing fails, prefer "unparsed but displayed" over throwing.
- **Option groups replace the old typed options.** `UnitConfiguration.ActiveGroups()` yields the groups in force: top-level groups that apply to the fielded profile, and under every taken choice the groups it unlocks (a standard bearer's magic standard, a champion's pistol). Command choices count as taken through `HasChampion`/`HasMusician`/`HasStandardBearer`; the battle standard through `Loadout.IsBattleStandardBearer`. Costing, equipment resolution, the roster and the editor all walk the same list.
- **Profiles are variants.** A character entry with several priced profiles (COMMANDERS: General and Captain) is one unit with `Unit.Profiles`; `UnitConfiguration.SelectedProfileId` picks which is fielded, and `appliesTo` on groups and choices filters against it. On a rank-and-file entry only the first priced profile is fielded; a priced handler line is not a variant.
- **Character mounts come from the book's mounts section.** A mount choice's `unitId` names the mount's own entry; `Unit.MountOptions` holds its resolved model and the troop type the rider becomes (War Beast → Cavalry, Monstrous Beast → Monstrous Cavalry, Monster → Monster). `UnitConfiguration.MountProfile`/`TroopType` follow the selection. A cavalry unit's mount still comes from its own profile block.
- **Magic items and families.** Item categories are the bundle's six: `weapon`, `armour`, `talisman`, `arcane`, `enchanted`, `standard` (`MagicItemCategories`). A `magicItems` group may name `itemTypes` (only those lists) and `families` (upgrade families the same allowance pays for — "one Virtue and/or Magic Items"); `UnitConfiguration.MagicItemSpend` adds those family picks to the budget. Dwarf runes are the `runic-items` family, not items; master runes stay once per army.
- **Faction upgrade pricing.** An entry's cost is flat or tiered ("Characters: free, Cavalry/Infantry: 1 point per model"); `FactionUpgrades.ResolveCost` picks the tier for the unit taking it. A family a group offers with its own rate ("+2 pts/model each") is charged at that rate per pick instead.
- **Adding a game system** is a data exercise: create `wwwroot/data/{key}/` with `{key}.json` (a formatVersion 2 bundle), `core-rules.json` and `troop-types.json`, then add the system to `GameSystems.All` (`Models/Domain/GameSystem.cs`). All systems share the nine-stat M/WS/BS/S/T/W/I/A/Ld profile. Saved army lists carry a `system` field and Builder autosaves are namespaced per system.
- **Character categories come from the bundle.** `section` plus `tier` and `named` map to `Lords`/`Heroes`/`Characters`/`NamedCharacters`; `UnitCategoryExtensions.IsCharacterCategory()` is the check to use for "is this a character unit". WAR has one characters section, so it declares no Lords/Heroes caps.
- **Troop type figures are per-system data**, in the hand-maintained `wwwroot/data/{system}/troop-types.json`: minimum rank width, supporting attacks (rider, mount and no-split-profile), inherent Impact Hits, Unit Strength and inherent Swiftstride. The rulebook's Troop Types chapter is prose, so the figures are read off it by hand. `GameDataRepository` throws on a missing row rather than falling back.
- **The engine's charts are per-system data too**, in `core-rules.json`, because the bundle's rulebook is prose with a handful of tables. `BundleDataTests.CoreRules_ChartsMatchTheRulebookText` holds the to-hit and to-wound charts to the chart printed in the bundle's rulebook chapter, so the two cannot drift.
- **Composition is data.** Percentages, the duplicate chart and the books' unit constraints all come from the bundle; `CompositionRules.FromData` builds them and `CompositionRules.Resolve` is the one place per-list overrides (`CompositionOverrides`, a percentage or a flat points figure per limit) take precedence. The validator and the Builder's progress bars both go through it.
- **The bundle is generated, not hand-written.** Fix data issues in `warhammer-armies-revamped` (its Typst sources and `export.py`), re-export with `python export.py --check --out ../Warhammer_Calculator_Edition/Warhammer/wwwroot/data/war/war.json`, and commit the copy. Known upstream gaps as of this revision: the rulebook's common items carry no ids (the loader derives one from the name), 26 units' special-rules strings are cut off mid-parenthesis (the parser drops the dangling fragment), and 25 option lines are `unclassified` and render as plain toggles.
- **Portraits**: run `tools/PortraitFetcher` (console app; supports per-faction and `--dry-run` args) to fetch PNGs from the Total War fandom wiki and regenerate `portrait-manifest.json`. Folders and manifest keys are faction ids. `PortraitService.Slugify` must stay in sync with the fetcher's filename slugging; the bundle's upper-case unit names slug to the same names as before.
- **Builder state vs simulation state.** `UnitConfiguration.Loadout` (an `ArmyLoadout`) holds army-builder-only state: custom name, wizard level/lore/spells, magic banner, General/BSB flags, faction upgrade picks. The combat engine never reads `Loadout`. Simulation-relevant choices (model count, weapon, command group, option selections, modifiers) live directly on `UnitConfiguration`.
- **Faction abilities are modifiers.** Shadow Dances and Tree Aspects (Wood Elves) compile to `UnitModifier` instances via `UnitConfiguration.FactionAbilityModifiers`, flowing through `EffectiveSpecialRules`/`AggregateModifier` like magic items. The Calculator has no faction-specific enums — add new faction abilities the same way, not as engine special cases. (Tree Whack remains a rule-flag-gated attack mode in `AttackResolver`.)
- **JS interop is minimal**: only `wwwroot/js/army-builder.js` (`window.armyBuilder`) for JSON file download, localStorage and `window.print()`. Builder autosaves the single active list to localStorage. Both localStorage keys are namespaced per game system and defined in one place, `ArmyListStorage` — the Builder writes the print key on the way to `/roster` rather than letting that page read the autosave, which is fire-and-forget and which a rename never triggers.
- **Styling**: Bootstrap 5 via CDN in `index.html`, dark-theme cards (`bg-dark text-light border-secondary`), custom rules in `wwwroot/css/app.css`. No CSS framework tooling.
- **Printing is a print stylesheet, not a PDF library.** `/roster` is a light-on-white page and `app.css` carries an `@media print` block plus an `@page` A4 rule, so the browser's "Save as PDF" destination produces the file. Anything screen-only carries `no-print`. Keep roster logic in `ArmyRosterBuilder` (tested) rather than in the Razor page (not tested).
- **Testing style**: deterministic tests use fixed-seed `Random` or direct resolver calls; probabilistic tests assert Monte Carlo results within tolerance bands. `TestHelpers.MakeUnit` builds a fixture unit with real weapon/armour/troop-type data; `TestHelpers.Equipment/Command/MagicItems/BattleStandard/WizardLevel/FactionUpgrade/Mounts` build option groups. `TestHelpers.FindUnitAsync("empire", "HALBERDIERS")` fetches a real unit — names are the books' running headers, in capitals. `BuilderFlowTests` walks the Builder's flow end to end on shipped data.

## Docs Map (two folders, different purposes)

- `Warhammer/docs/00-*.md … 06-*.md` — **design/spec** documents written before implementation (roadmap, architecture, data format, combat rules, combat audit, army builder plan). `02-data-format.md` describes the current bundle; the others carry a note where the code has moved past them.
- `docs/*.md` (repo root) — **descriptive** docs of the implementation (overview, structure, combat-engine, data-models, components, limitations).
- `docs/limitations.md` — list of not-yet-implemented mechanics; check it before assuming a rule is simulated.

## Known Blind Spots

- No UI/component tests — only the C# layer is tested. `BuilderFlowTests` covers the services the Builder page calls, not the Razor markup.
- Army builder scope: single faction, no allies, characters are standalone entries (no unit attachment). Handler ratios inside a unit ("one Handler per 5 War Hounds") are not validated.
- Simulation covers close combat; shooting/magic phases are not simulated (magic items/spells matter only via stat/rule modifiers).
