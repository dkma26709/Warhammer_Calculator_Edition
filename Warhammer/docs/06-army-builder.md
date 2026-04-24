# Army Builder — Design & Roadmap

## Goal

A standalone army list manager for WAP. User picks a faction and points target, adds and configures units, and exports the finished list as JSON. Full WAP composition rules are validated.

## Scope — v1

**In scope:**
- Single-faction lists (no allies)
- Full WAP percentage composition rules (Lords, Heroes, Core, Special, Rare)
- Character loadouts: equipment, mount, wizard lore, magic items
- Magic banners on units with standard bearers
- JSON export / import + browser LocalStorage autosave (single active list)
- Default points targets (1000 / 1500 / 2000 / 2500 / 3000) plus custom integer

**Out of scope for v1:**
- Allies / multi-faction lists
- Integration with the matchup or gauntlet simulators (can come later)
- Character-to-unit attachment (characters are standalone list entries in v1)
- Faction-specific composition quirks (e.g. Dwarf Slayers-as-Core conditions, OnG "largest block" rule). Baseline percentages only; quirks surface as warnings, not errors.
- Multiple saved lists (only one active list at a time)

## Data Foundation (already in place)

| Data | Location | What we use |
|---|---|---|
| Unit category | `factions/{f}.json` → `units[].category` | Lords / Heroes / Core / Special / Rare / CharacterMounts |
| Points cost | `units[].profiles[].points` | Per-model cost |
| Equipment options | `units[].options[]` | Already parsed |
| Character mounts | `category: "CHARACTER MOUNTS"` entries | Merge into character config |
| Magic item budgets | `options[]` with `type: "magic_items"` and `pointsLimit` | Budget enforcement |
| Magic items | `factions/{f}-extras.json` + `common-magic-items.json` | Categories: magic_weapon, magic_armour, talisman, arcane_item, enchanted_item, magic_standard |
| Lores | `factions/{f}.json` lore entries | Wizard lore dropdown |

## Composition Rules (WAP — confirmed)

### Category percentages

| Slot | Limit |
|---|---|
| Lords | max 25% |
| Heroes | max 25% |
| **Lords + Heroes combined** | **max 35%** |
| Core | **min 25%** |
| Special | max 50% |
| Rare | max 25% |

### General
- At least one character (Lord or Hero) required. Acts as General.

### Per-army unit limits (`0-X`)
Some units have hard caps — `0-1 per army`, `0-2 per army`, etc. Parsed from unit JSON `notes` field. Enforced as errors.

### Duplicate limits for Special and Rare
Duplicate = same base unit type, regardless of size or optional equipment.

| Points | Special duplicates | Rare duplicates |
|---|---|---|
| 0–999 | 1 | 1 |
| 1000–1999 | 2 | 1 |
| 2000–2999 | 3 | 1 |
| 3000–3999 | 4 | 2 |
| 4000–4999 | 5 | 3 |
| each +1000 | +1 | +1 |

**Character mount caveat**: if a character's mount is a Monstrous Creature, Monster, Chariot, Shrine or War Machine available as a Special or Rare choice, the mount counts toward the duplicate limit for its category. v1 may imperfectly detect this intersection — surface as warning when ambiguous, error when clear.

## Domain Model

```csharp
// New
public record ArmyList
{
    public FactionKey Faction { get; init; }
    public int PointsTarget { get; init; }
    public string Name { get; init; } = "Untitled List";
    public List<UnitConfiguration> Units { get; init; } = new();
}

public record ValidationIssue(
    ValidationSeverity Severity,  // Error | Warning
    string Code,                   // e.g. "CORE_BELOW_MIN"
    string Message,
    UnitConfiguration? RelatedUnit = null);

public static class ArmyCompositionRules
{
    public const double LordsMaxPercent = 0.25;
    public const double HeroesMaxPercent = 0.25;
    public const double CharactersCombinedMaxPercent = 0.35;
    public const double CoreMinPercent = 0.25;
    public const double SpecialMaxPercent = 0.50;
    public const double RareMaxPercent = 0.25;

    // Duplicates allowed per Special/Rare unit type, keyed by points bracket.
    // (pts, specialDupes, rareDupes): 0-999 → 1/1, 1000-1999 → 2/1, 2000-2999 → 3/1,
    // 3000-3999 → 4/2, 4000-4999 → 5/3, then +1 each per +1000.
    public static (int SpecialDupes, int RareDupes) DuplicateLimits(int pointsTarget);
}

public class ArmyListValidator
{
    public List<ValidationIssue> Validate(ArmyList list);
}
```

Characters track mount + lore + magic items through extensions to existing `UnitConfiguration` (or via a nested record — TBD in phase 3).

## UI Flow

1. **Builder page** (`/builder`)
2. Select faction + points target (presets or custom)
3. Unit browser on the left — grouped by category, click to add
4. Active list on the right — each entry opens the existing `UnitConfigurator` for editing
5. Bottom panel: points bar per category + validation messages (errors block export, warnings don't)
6. Header actions: new / export / import / autosave indicator

## Persistence

**Export format** (example):
```json
{
  "schema": "wap-army-list/v1",
  "faction": "dwarfs",
  "pointsTarget": 2000,
  "name": "Thorgrim's Host",
  "units": [
    { "unitKey": "Lord", "modelCount": 1, "frontage": 1, "equipment": [...], "magicItems": [...], "mount": "Shieldbearers", "lore": null },
    ...
  ]
}
```

Autosave: browser LocalStorage under key `warhammer.armylist.active`. Overwrites on every change. No history, no multi-slot.

Import: file upload → validate schema → replace active list.

## Phases

### Phase 1 — Foundation (no UI)
- `ArmyList`, `ValidationIssue`, `ArmyCompositionRules` types
- `ArmyListValidator` service covering all percentage rules + general requirement
- xUnit tests covering each rule in isolation and a "clean list" happy path

### Phase 2 — Builder UI skeleton
- `/builder` route + page
- Faction + points pickers
- Unit browser grouped by category
- Active list with add / edit / duplicate / remove, reusing `UnitConfigurator`
- Points summary per category (bars showing used vs cap)
- Validation panel

### Phase 3 — Character loadouts
- Mount dropdown on character configs (sourced from faction's CharacterMounts)
- Wizard lore dropdown
- **Spell selection at army-creation time** (picks individual spells from chosen lore, subject to wizard level — store on character, surface at simulation time)
- Magic item budget indicator + cap enforcement (data is already on characters)

### Phase 4 — Magic banners
- Banner slot on units with Standard Bearer
- Source from `magic_standard` items (common + faction)
- Per-banner point caps (WAP typically limits banner cost per unit)

### Phase 5 — Persistence
- JSON export / import
- LocalStorage autosave
- Import validation (schema version, faction match, unknown units → warnings)

## Open Questions / Risks

- **WAP percentages** documented above need user verification against the WAP rulebook PDF.
- **Composition-rules-in-code vs JSON**: starting in code for speed. If rules move or become faction-specific, promote to `wwwroot/data/composition-rules.json`.
- **0-1 per army detection**: requires scanning unit `notes` text for phrases like "0-1 per army". May need a structured field added during re-extraction.
- **Character mount points**: mount entries have their own profiles; need to ensure character total points correctly include mount cost.
