# Data Models

## JSON Data Files

Every system ships three files under `wwwroot/data/{system}/`. Only `war` ships today.

### `{system}.json` — the bundle

The whole edition in one file, formatVersion 2, exported by `export.py` in the
`warhammer-armies-revamped` repo and copied here on a release. Deserialised by `BundleService`
into `BundleData`; each faction stays a raw `JsonElement` until a page asks for it, then becomes a
`FactionData`.

```
formatVersion, system, version, source     — identity; source is the export's git commit
rulebook.rules                              — {"ALWAYS STRIKES FIRST": "text", ...}
rulebook.items                              — common magic items (category, name, cost, only, text)
rulebook.lores[].spells[]                   — name, level ("1".."4", "Signature Spell", "Lore Attribute"), cast, text
composition                                 — pointsCategories, singleUnitMaxPercent, general, battleStandard,
                                              specialCharacters, duplicates (bands + beyond), sources
factions.{id}                               — one army book, see below
```

A faction:

```
id, name, align                             — "empire", "The Empire", "order"
rules[]                                     — army special rules: id, name, text
items[]                                     — the book's magic items, same shape as the rulebook's, with ids
upgrades[]                                  — upgrade chapters: id, chapter, group, name, cost (number | tiers | null), only, text
lores[]                                     — the book's own lores
composition.unitConstraints[]               — max / limited / perSlot / ratio / general / handlers, each with raw
units[]                                     — see below
```

A unit:

```json
{
  "id": "halberdiers",
  "name": "HALBERDIERS",
  "section": "core",            "tier": null,          "named": false,
  "profiles": [{ "id": "halberdier", "name": "Halberdier", "M": 4, "WS": 3, "BS": 3, "S": 3, "T": 3, "W": 1, "I": 3, "A": 1, "Ld": 7, "points": 5 }],
  "unitSize": "15-45",          "unitSizeParsed": { "min": 15, "max": 45 },
  "troopType": "Infantry (Human)",
  "baseSize": "20x20 or 25x25",
  "equipment": "Polearm",       "equipmentList": ["Polearm"],
  "specialRules": "State Troops","specialRulesList": ["State Troops"],
  "optionGroups": [
    { "id": "armour", "kind": "equipment", "select": "one", "min": 0, "max": 1, "appliesTo": null,
      "raw": "May choose one of the following:",
      "choices": [
        { "id": "light-armour", "name": "Light armour", "points": 0.5, "perModel": true, "raw": "Light armour +0.5 point/model" },
        { "id": "medium-armour", "name": "Medium armour", "points": 1.5, "perModel": true, "raw": "Medium armour +1.5 points/model" }
      ] },
    { "id": "standard-bearer", "kind": "command", "select": "any", "role": "standardBearer",
      "raw": "May upgrade one Halberdier to a Standard Bearer +10 points",
      "choices": [ { "id": "standard-bearer", "name": "Standard Bearer", "points": 10,
        "options": [ { "id": "magic-standard", "kind": "magicStandard", "pointsLimit": 25, "choices": [ ... ] } ] } ] }
  ]
}
```

Group kinds: `equipment`, `mount`, `command`, `magicStandard`, `magicItems`, `battleStandard`,
`wizardLevel`, `factionUpgrade`, `specialRule`, `upgrade`. Selection modes: `one`, `any`, `count`.
A choice may carry `replaces`, `unitId` (a mount's own entry), `appliesTo` (a profile id),
`min`/`max`/`everyModels` (counted picks), `level`/`levelDelta` (wizard levels) and nested
`options`. The full contract is `warhammer-armies-revamped/docs/format.md`.

Fields the bundle writes in more than one shape (`specialRules`, `upgrades`, `notes`, an item's
`bound`, a profile's `points`) are read by the converters in `Models/Json/Converters.cs`.

### `core-rules.json` — the engine's tables (hand-maintained)

```
closeCombatToHit.table        — 10x10 string[][] (WS 1-10 vs WS 1-10), plain scores
toWound.table                 — 10x10 string[][] (S 1-10 vs T 1-10), "6+/2+" compound cells allowed
armourSaves.strengthModifiers — {"S3": 0, "S4": -1, "S5": -2, ...}
weapons.closeCombat           — {key: WeaponEntry} (handWeapon, spear, greatWeapon, ...)
armour                        — {key: ArmourEntry} (lightArmour, heavyArmour, shields, ...)
```

The bundle's rulebook prints the same charts as text; `BundleDataTests` holds this file to them.

### `troop-types.json` — the Troop Types chapter's figures (hand-maintained)

One row per `BaseTroopType`: minimum rank width, supporting attacks (rider, mount, no split
profile), inherent Impact Hits, Unit Strength, split profile, Swiftstride. Loading throws on a
missing row.

---

## C# Model Mapping

### JSON -> C# (Models/Json/)

| JSON | C# Record | Key Fields |
|-----------|-----------|------------|
| the bundle | `BundleData` | `FormatVersion`, `Source`, `Rulebook`, `Composition`, `Factions` (raw elements) |
| `rulebook` | `RulebookData` | `Rules`, `Items: List<MagicItemEntry>`, `Lores: List<LoreEntry>` |
| `composition` | `CompositionData` | `PointsCategories`, `SingleUnitMaxPercent`, `Duplicates`, `UnitConstraints` |
| a faction | `FactionData` | `Id`, `Name`, `Rules`, `Items`, `Upgrades: List<UpgradeEntry>`, `Lores`, `Composition`, `Units` |
| a unit | `UnitEntry` | `Id`, `Name`, `Section`, `Tier`, `Named`, `Profiles`, `UnitSizeParsed`, `TroopType`, `Equipment`, `SpecialRules`, `Magic`, `Notes`, `OptionGroups` |
| a profile | `ProfileEntry` | `Id`, `Name`, `M/WS/BS/S/T/W/I/A/Ld` (nullable int), `Points` (nullable) |
| an option group | `OptionGroup` / `OptionChoice` | see above |
| core-rules.json | `CoreRulesData` | `CloseCombatToHit`, `ToWound`, `ArmourSaves`, `Weapons`, `Armour` |
| troop-types.json | `TroopTypesData` | `Types` keyed by kebab-case troop type |

### Custom JSON Converters (`Models/Json/Converters.cs`)

| Converter | Purpose |
|-----------|---------|
| `NullableIntConverter` | Stat values: numbers, `"*"`, `"-"`, whitespace -> `int?` |
| `NullableDoubleConverter` | Points: numbers, `"-"`, `"free"` -> `double?` |
| `StringOrStringListConverter` | A string, a list of strings, or a list of `{name, text}` definitions -> one comma-joined string of names |
| `LenientStringConverter` / `LenientStringListConverter` | Any scalar, object (by `name`/`spell`/`text`) or array -> text |
| `StringOrNumberConverter` | A spell's level, `1` or `"Signature Spell"` -> text |
| `UpgradeCostConverter` | A number, or `[[scope, text], ...]` tiers -> `UpgradeCost` |

### Domain Models (Models/Domain/)

| Model | Built From | Purpose |
|-------|-----------|---------|
| `Faction` | a faction's header | `Id`, `Name`, `Align` — the faction list the pages show |
| `Unit` | `UnitEntry` | The resolved entry: base model, priced `Profiles`, `MountOptions` (from the book's mounts section), `OptionGroups`, command availability, the raw entry |
| `UnitConfiguration` | `Unit` | User config: fielded profile, `OptionSelections` by group and choice id, command, magic items, `Loadout`; computed combat stats and points |
| `OptionSelections` | — | Group id → choice id → count |
| `CompositionRules` | `CompositionData` | Percentages, the duplicate chart (`DuplicateRule`), the book's constraints; `Resolve` applies a list's overrides |
| `FactionUpgrades` | `UpgradeEntry` | Family ids from chapter titles, flat or tiered cost resolution |
| `MagicItemAllowance` | a `magicItems` group | Points limit, admitted item lists, families the allowance pays for |
| `ParsedSpecialRules` | `UnitEntry.SpecialRules` | 70 typed properties for combat rules; scoped rules routed to the mount |
| `ParsedWeaponRules` | `WeaponEntry.SpecialRules` + `Notes` | Weapon modifiers (initiative, extra attacks, MB, FiER, parry) |
| `TroopTypeInfo` | `UnitEntry.TroopType` + `troop-types.json` | Base type enum + combat properties (rank width, support attacks, unit strength) |

### Saved lists (`ArmyListDto`, schema `army-list/v2`)

`faction` is the faction id; each unit carries `unitId` (and `unitName` as a fallback), the
fielded `profileId`, `selections` as group id → choice id → count, magic items and the banner by
id, and `factionUpgrades` as family → entry ids. Costs of upgrade picks are re-resolved from the
book on import.
