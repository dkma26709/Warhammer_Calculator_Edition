# Data Format Specification

## Overview

Every game system ships three files under `wwwroot/data/{system}/`. The app reads them at
runtime over HTTP and never touches the books' sources.

| File | Made by | Content |
|---|---|---|
| `{system}.json` | `export.py` in `warhammer-armies-revamped`, copied here on a release | **The bundle**: every army book, the rulebook's rules, magic items and lores, and the composition rules. formatVersion 2. Generated; never hand-edited |
| `core-rules.json` | hand-maintained | The combat engine's tables: to-hit and to-wound charts, armour save modifiers, close-combat weapons, armour |
| `troop-types.json` | hand-maintained | The Troop Types chapter's figures per base troop type |

**Source of truth for the bundle**: the Typst sources in `warhammer-armies-revamped/src/` →
`python export.py --check` → `build/war.json` → copied to `wwwroot/data/war/war.json`. The
bundle's `source` field is the commit it was exported from, and `WarBundleTests` guards the copy.

The full contract is `warhammer-armies-revamped/docs/format.md` and its JSON Schema
`schema/war.schema.json`. This page covers what the app reads and how.

## The bundle

### Top level

```jsonc
{
  "formatVersion": 2,
  "system": "war",
  "version": "1.0",
  "source": "<git sha>",
  "rulebook": { "name", "version", "rules", "items", "lores", "weapons", "chapters" },
  "composition": { ... },
  "factions": { "empire": { ... }, "orcs-goblins": { ... } }
}
```

`BundleService` deserialises the top level into `BundleData` and keeps each faction as a raw
element until a page asks for it, then deserialises that one into `FactionData` and caches it.
The file is 7 MB; one fetch per session.

### Composition

```jsonc
{
  "pointsCategories": {
    "characters": { "maxPercent": 0.35 },
    "core": { "minPercent": 0.25, "maxPercent": null, "expendable": { "requiresOtherCore": 1 } },
    "special": { "maxPercent": 0.5 },
    "rare": { "maxPercent": 0.25 }
    // "lords" and "heroes" appear only for a book that splits its characters
  },
  "singleUnitMaxPercent": 0.25,
  "general": { "required": true },
  "battleStandard": { "max": 1 },
  "specialCharacters": { "maxEach": 1 },
  "duplicates": {
    "special": { "bands": [ { "minPoints": 0, "maxPoints": 999, "max": 1 }, ... ],
                 "beyond": { "everyPoints": 1000, "add": 1 } },
    "rare": { ... }
  },
  "sources": { "singleUnitMaxPercent": "No single character or unit ... 25% ...", ... }
}
```

`CompositionRules.FromData` turns this into the domain rules. A faction's own
`composition.unitConstraints` are appended when the Builder loads that faction:

| `type` | wording | fields |
|---|---|---|
| `max` | "You may not have more than 2 Arch Lectors" | `max`; `profileId` when one profile of the unit is counted |
| `limited` | "0-1", "0-1 per 1000 points" | `min`, `max`, `perPoints` |
| `perSlot` | "1-2 Bolt Throwers as a single Rare choice" | `perSlot: {min, max}`, `section` |
| `ratio` | "not more units of X than of Y" | `max`, `perUnit`, `requires` (unit ids), `requiresAny`, `requiresRule` |
| `general` | "must / may never be the Army General" | `mustBeGeneral`, `mayBeGeneral` |
| `handlers` | "one Handler for every 5 War Hounds" | `handlers: {name, per}`, `profileId` (not validated) |

### Faction

```jsonc
{
  "id": "empire", "name": "The Empire", "version": "1.0", "align": "order",
  "rules":    [ { "id", "name", "text" } ],
  "items":    [ { "id", "category", "name", "cost", "type", "only", "bound", "oneUse", "common", "text" } ],
  "upgrades": [ { "id", "chapter", "group", "name", "cost", "only", "text" } ],
  "lores":    [ { "name", "spells": [ { "name", "level", "cast", "text" } ] } ],
  "composition": { "unitConstraints": [ ... ] },
  "units":    [ ... ]
}
```

- Item `category` is one of `weapon`, `armour`, `talisman`, `arcane`, `enchanted`, `standard`.
- An upgrade's `cost` is a number, `null`, or tiers `[["Characters", "free"], ["Cavalry/Infantry", "1 point per model"]]`;
  `FactionUpgrades.ResolveCost` picks the tier for the unit taking it. Its family id is the
  chapter title in kebab-case (`FactionUpgrades.FamilyOf`).
- The rulebook's `lores` are offered after the faction's own (`GetLoresAsync`).

### Unit

```jsonc
{
  "id": "commanders", "name": "COMMANDERS", "chapter": "CHARACTERS",
  "section": "characters", "tier": null, "named": false,
  "profiles": [
    { "id": "general", "name": "General", "M": 4, ..., "points": 95 },
    { "id": "captain", "name": "Captain", "M": 4, ..., "points": 55 }
  ],
  "unitSize": null, "unitSizeParsed": null,
  "troopType": "Infantry (Character, Human)", "baseSize": "20x20 or 25x25",
  "equipment": "Hand weapon", "equipmentList": ["Hand weapon"],
  "specialRules": "...", "specialRulesList": [ ... ],
  "magic": null, "notes": [ ... ],
  "optionGroups": [ ... ]
}
```

- `section` + `tier` + `named` → `UnitCategory` (`UnitCategoryExtensions.FromSection`). The
  `mounts` section holds the entries mount choices point at.
- Several priced profiles on a character entry are variants; `UnitConfiguration.SelectedProfileId`
  picks one. A rank-and-file entry fields its first priced profile; an unpriced second profile is
  the unit's mount.
- `specialRules`, `upgrades` and `notes` may be a string, a list, or a list of `{name, text}`
  definitions; a profile's `points` may be `"-"`; an item's `bound` may be `true` or an object.
  `Models/Json/Converters.cs` reads them all.

### Option groups

```jsonc
{
  "id": "mount", "kind": "mount", "select": "one", "min": 0, "max": 1, "appliesTo": null,
  "raw": "May be mounted on one of the following:",
  "choices": [
    { "id": "warhorse", "name": "Warhorse", "points": 15, "perModel": false, "raw": "Warhorse +15 points", "unitId": "warhorse" },
    { "id": "griffon", "name": "Griffon", "points": 125, "perModel": false, "raw": "Griffon (General only) +125 points",
      "appliesTo": "general", "unitId": "griffon" }
  ]
}
```

| `kind` | what the app does with it |
|---|---|
| `equipment` | dropdown / checkboxes; choices resolve through `EquipmentParser` to weapon and armour keys; `replaces` removes the replaced item |
| `mount` | dropdown; a choice's `unitId` resolves to the book's mounts entry, giving the rider a mount profile and troop type |
| `command` | the command group checkboxes (`role`: champion / musician / standardBearer); nested `options` under the choice apply once taken |
| `magicStandard` | the banner slot; `pointsLimit` null means no limit |
| `magicItems` | the magic item allowance for `appliesTo`; `pointsLimit`, `itemTypes`, `families` |
| `battleStandard` | the BSB toggle and its `points` |
| `wizardLevel` | a costed level pick; the level itself is set on the loadout |
| `factionUpgrade` | a multi-select over the `family`'s entries, with an optional `pointsLimit` or a per-pick rate |
| `specialRule`, `upgrade` | costed toggles |

`select` is `one`, `any` or `count`; a counted choice carries `min`/`max` and `everyModels`.
`UnitConfiguration.ActiveGroups()` is the one walk over the tree; nested groups appear once
their parent choice is taken.

## `core-rules.json`

```jsonc
{
  "name": "Warhammer Armies Revamped - Core Rules",
  "closeCombatToHit": { "table": [["4+", "4+", "5+", ...], ...] },   // 10×10, plain scores
  "toWound":          { "table": [["4+", "5+", "6+", "6+/2+", ...], ...] },   // compound cells allowed
  "armourSaves":      { "strengthModifiers": { "S1": 0, ..., "S4": -1, ..., "S10": -7 } },
  "weapons": { "closeCombat": { "handWeapon": { "name", "range", "strength", "specialRules", "notes" }, ... } },
  "armour":  { "lightArmour": { "name", "combatSave": "+1/6+", "missileSave": "+1/6+", "specialRules" }, ... }
}
```

`BundleDataTests.CoreRules_ChartsMatchTheRulebookText` parses the chart printed in the bundle's
ROLL TO HIT and ROLL TO WOUND chapters and holds this file to it.

## `troop-types.json`

```jsonc
{
  "source": "Warhammer Armies Revamped rulebook, Troop Types chapter",
  "notes": ["..."],
  "types": {
    "monstrous-cavalry": {
      "minRankWidth": 3,
      "supportingAttacks": 1,          // the rider's figure
      "mountSupportingAttacks": 0,     // mounts make no supporting attacks
      "nonSplitSupportingAttacks": 3,  // stated separately, not rider + mount
      "impactHits": 0,                 // per front-rank model, from the troop type alone
      "unitStrength": 3,
      "splitProfile": true,
      "swiftstride": false             // no troop type grants it
    }
    // ... one row per BaseTroopType, keyed by BaseTroopTypeExtensions.ToJsonKey()
  }
}
```

`GameDataRepository.ResolveTroopTypesAsync` requires a row for every `BaseTroopType` and throws
naming the system and the missing key, so a new system that skips the file fails on load rather
than silently inheriting another edition's numbers.

## Saved army lists (`army-list/v2`)

```jsonc
{
  "schema": "army-list/v2", "system": "war", "faction": "empire",
  "pointsTarget": 2000, "name": "...",
  "units": [ {
    "unitId": "commanders", "unitName": "COMMANDERS", "profileId": "captain",
    "modelCount": 1, "frontage": 1, "selectedWeapon": "handWeapon",
    "hasChampion": false, "hasMusician": false, "hasStandardBearer": false,
    "selections": { "mount": { "warhorse": 1 } },
    "magicItems": [ { "id": "giant-blade", "name": "GIANT BLADE", "category": "weapon", "pointsCost": 45, "modifier": { ... } } ],
    "factionUpgrades": { "knightly-orders": ["reiksguard-knights"] },
    "wizardLevel": 1, "selectedLore": null, "selectedSpells": [],
    "isGeneral": true, "isBattleStandardBearer": false
  } ],
  "composition": { ... overrides, when any ... }
}
```

Units resolve by `unitId`, falling back to `unitName` with a warning; faction upgrade costs are
re-resolved from the book on import.
