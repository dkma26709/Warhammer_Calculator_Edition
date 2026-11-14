# Data Format Specification

## Overview

All game data comes from WAP PDFs via Python extractors. The app reads JSON files at runtime — it never accesses PDFs directly. Data files are kept separate: unit data and extras data are not merged.

**Source of truth**: WAP PDFs → Python extractors → JSON files → Blazor app

## File Inventory

### Core Rules
| File | Source | Size | Content |
|------|--------|------|---------|
| `core-rules.json` | `extract_core_rules.py` | ~74KB | Combat tables, weapons, armour, 50+ special rules, troop types |

### Per-Faction Files (× 15 factions)
| File Pattern | Source | Content |
|-------------|--------|---------|
| `{faction}.json` | `extract_pdf.py` | All units for the faction (profiles, equipment, options, special rules) |
| `{faction}-extras.json` | `extract_army_extras.py` | Army special rules, magic items, lores of magic, faction upgrades |

### Faction Names
`beastmen`, `bretonnia`, `daemons-of-chaos`, `dark-elves`, `dwarfs`, `high-elves`, `lizardmen`, `ogre-kingdoms`, `orcs-and-goblins`, `skaven`, `the-empire`, `tomb-kings`, `vampire-counts`, `warriors-of-chaos`, `wood-elves`

### App Data Location
```
wwwroot/data/
├── core-rules.json
└── factions/
    ├── lizardmen.json
    ├── lizardmen-extras.json
    └── ...
```

## JSON Schemas

### core-rules.json

```json
{
  "name": "Warhammer: The Game of Fantasy Battles - 9th Edition v2.32",

  "closeCombatToHit": {
    "description": "...",
    "table": [[4,4,5,...], ...],          // 10×10 array, [attackerWS-1][defenderWS-1] → D6 score needed
    "tableAxes": { "rows": "Attacker's WS (1-10)", "columns": "Defender's WS (1-10)" }
  },

  "toWound": {
    "description": "...",
    "table": [[...], ...],                // 10×10 array, [strength-1][toughness-1] → D6 score needed
    "tableAxes": { "rows": "Attacker's Strength (1-10)", "columns": "Target's Toughness (1-10)" }
  },

  "shootingToHit": {
    "description": "...",
    "table": { "BS1": 6, "BS2": 5, ... "BS10": 1 },
    "modifiers": {}
  },

  "armourSaves": {
    "description": "...",
    "strengthModifiers": { "S1": 0, "S2": 0, "S3": 0, "S4": -1, "S5": -2, ... "S10": -7 }
  },

  "weapons": {
    "closeCombat": {
      "handWeapon": { "name": "HAND WEAPON", "range": "Combat", "strength": "As user", "specialRules": null, "notes": "..." },
      "spear":      { "name": "SPEAR",       "range": "Combat", "strength": "As user", "specialRules": "+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*", "notes": "..." },
      // ... 9 close combat weapons total
    },
    "missile": {
      "bow":       { "name": "BOW",       "range": "24\"", "strength": "3", "specialRules": "Multiple Shots (2)*, Volley Fire", "notes": "..." },
      "crossbow":  { "name": "CROSSBOW",  "range": "30\"", "strength": "4", "specialRules": "Ponderous" },
      // ... 14+ missile weapons total
    }
  },

  "armour": {
    "lightArmour":  { "name": "LIGHT ARMOUR",  "combatSave": "+1/6+", "missileSave": "+1/6+", "specialRules": null },
    "heavyArmour":  { "name": "HEAVY ARMOUR",  "combatSave": "+3/4+", "missileSave": "+3/4+", "specialRules": null },
    "shields":      { "name": "SHIELDS",        "combatSave": "+1/6+", "missileSave": "+1/6+", "specialRules": "Parry (6+)", "notes": "..." },
    // ... 6 armour types total (light, medium, heavy, shields, buckler, barding)
  },

  "commonSpecialRules": {
    "killingBlow": { "name": "KILLING BLOW", "parameterized": false, "description": "..." },
    "armourPiercing": { "name": "ARMOUR PIERCING", "parameterized": true, "description": "..." },
    // ... 50+ rules. "parameterized" indicates whether the rule takes a numeric value in brackets.
  },

  // Prose only — the descriptive text from the Troop Types chapter, keyed by heading.
  // The engine reads NONE of this. The figures it needs live in troop-types.json (below),
  // because the chapter is prose rather than a ruled table and cannot be extracted.
  "troopTypes": {
    "ranks": { "name": "RANKS", "description": "A unit of Infantry is required to be five or more models wide..." },
    "supportingAttacks": { "name": "SUPPORTING ATTACKS", "description": "An Infantry model may make up to one supporting attack..." },
    "unitStrength": { "name": "UNIT STRENGTH", "description": "Infantry have a Unit Strength of 1..." },
    // ... troop type definitions and rules
  }
}
```

### `troop-types.json` (hand-maintained, per system)

The combat-relevant troop type figures, which differ between editions. Not generated — see
`AGENTS.md` and `docs/03-combat-rules.md` for the values and the divergences.

```jsonc
{
  "source": "... 9th Edition v3.11, Troop Types chapter (p71-76)",
  "notes": ["why this file is hand-maintained, and what diverges from the other edition"],
  "types": {
    "monstrous-cavalry": {
      "minRankWidth": 3,
      "supportingAttacks": 1,          // the rider's figure
      "mountSupportingAttacks": 0,     // zero throughout wap-v3
      "nonSplitSupportingAttacks": 3,  // stated separately, not rider + mount
      "impactHits": 0,                 // per front-rank model, from the troop type alone
      "unitStrength": 3,
      "splitProfile": true,
      "swiftstride": false             // no wap-v3 troop type grants it
    }
    // ... one row per BaseTroopType, keyed by BaseTroopTypeExtensions.ToJsonKey()
  }
}
```

Deserialized to `TroopTypesData`; `GameDataRepository.ResolveTroopTypesAsync` requires a row for
every `BaseTroopType` and throws naming the system and the missing key, so a new system that
skips the file fails on load rather than silently inheriting another edition's numbers.

### {faction}.json (Unit Data)

```json
{
  "factionName": "Lizardmen",
  "units": [
    {
      "name": "Saurus Warriors",
      "category": "Core",                    // Lords, Heroes, Core, Special, Rare
      "profiles": [
        {
          "name": "Saurus Warrior",
          "M": 4, "WS": 3, "BS": 0, "S": 4, "T": 4, "W": 1, "I": 1, "A": 2, "Ld": 8,
          "points": 11                        // per-model cost (null for secondary profiles)
        }
      ],
      "troopType": "Infantry",               // May include flags: "Infantry (Character, Human)"
      "baseSize": "25x25",                    // "WxH" mm. May be "20x20 or 25x25"
      "equipment": "Hand weapon, light armour, shield",
      "options": [
        "May upgrade one Saurus Warrior to a Spawn Leader +10 points",
        "May have spears +1 point/model"
      ],
      "specialRules": "Cold-blooded, Predatory Fighter, Natural Armour (4+)",
      "unitSize": "10+",                     // "N+", "N-M", or "1" (characters)
      "pointsCost": 110,                     // base unit cost
      "magic": null,                         // wizard spell access (if applicable)
      "upgrades": null,                      // upgrade descriptions
      "notes": null,                         // additional rules text
      "crew": null,                          // war machine crew
      "mount": null,                         // mount options
      "drawnBy": null                        // artist credit
    }
  ]
}
```

### {faction}-extras.json (Army Extras)

```json
{
  "factionName": "lizardmen",
  "armySpecialRules": [
    {
      "name": "Predatory Fighter",
      "description": "Whenever a model with this special rule rolls a 6 To Hit in close combat..."
    }
  ],
  "magicItems": [
    {
      "name": "Scimitar Of The Sun Resplendent",
      "points": "45",                        // string (not int)
      "category": "magic_weapon",            // magic_weapon, magic_armour, talisman, arcane_item, enchanted_item, magic_standard
      "description": "This weapon gives the wielder +2 Attacks and the Hatred special rule.",
      "restriction": null                    // optional: "Skink High Chief on Stegadon only."
    }
  ],
  "loresOfMagic": [
    {
      "name": "Geomancy",
      "attribute": "Every time a spell from the Lore of Geomancy is successfully cast...",
      "spells": [
        {
          "name": "Earth Line",
          "level": 0,                        // 0 = signature spell
          "castValue": 8,
          "description": "...",
          "type": "special"                  // augment, hex, direct_damage, magic_missile, special
        }
      ]
    }
  ],
  "factionUpgrades": [
    {
      "name": "Blessed Spawning Of Chotec",
      "points": "10",                       // string
      "description": "Models with this Blessed Spawning re-roll failed Charge and Pursuit distances."
    }
  ]
}
```

## Field Conventions & Parsing Notes

### Stats (`M`, `WS`, `BS`, `S`, `T`, `W`, `I`, `A`, `Ld`)
- Type: `int?` — can be integer, null, or originally `"*"` (variable)
- First profile with a `points` value = primary profile (the fighter)
- Second profile (if exists) = mount profile (uses mount's M, and mount's WS/S/I/A for mount attacks)
- Need a custom JSON converter for `"*"` → null

### `specialRules` Field
- Comma-separated names: `"Cold-blooded, Predatory Fighter, Natural Armour (4+)"`
- May contain `\n` from PDF extraction (e.g., `"Killing Blow, Natural Armour (5+)\nHARPIES"`)
- Parenthetical values: `Natural Armour (5+)`, `Regeneration (4+)`, `Ward Save (4+)`, `Fly (7)`, `Armour Piercing (1)`
- Faction-specific qualifiers: `Hatred (Vampire Counts)`, `Killing Blow (Casket Guard only)`
- Parse with regex on the full string (don't split by newline first)

### `baseSize` Field
- Format: `"WxH"` mm — e.g., `"25x25"`, `"50x100"`
- Multiple options: `"20x20 or 25x25"` — use first number as frontage width
- Default if missing: 25mm

### `troopType` Field
- Simple: `"Infantry"`, `"Cavalry"`, `"Monster"`
- With flags: `"Infantry (Character, Human)"`, `"Chariot (Armour Save 4+)"`
- Parse into base type enum + character flag + any intrinsic armour save

### `equipment` Field
- Comma-separated: `"Hand weapon, light armour, shield"`
- Weapon/armour names should match core-rules.json keys after normalization

### `options` Field
- Free-text strings describing upgrades
- Contain weapon names, point costs, command group upgrades
- For iteration 1: parse point costs and weapon/armour names; complex options deferred

### `points` Field in Magic Items
- Stored as string, not integer (e.g., `"45"` not `45`)
- Convert to int during deserialization
- A points string (magic items and faction upgrades alike) is a plain number, cumulative `"15/25/50"` (each figure the price of one further copy, so at most that many copies: 15, 40, 90 in total), or tiered per troop type (`"Characters free\nCavalry 1 point per model"`)

### `description` Fields
- May contain `\n` line breaks from PDF column extraction
- Treat as prose text — display as-is or normalize whitespace

## Data Pipeline

```
WAP PDFs (source of truth)
    ↓ Python extractors
Extracted/json/*.json (extraction output)
    ↓ Manual copy (or script)
wwwroot/data/ (app reads at runtime)
    ↓ HttpClient + System.Text.Json
C# models (in-memory)
```

The extractors are separate from the .NET project. The copy step populates the app's static data directory.

---

# Pass 2 — Implementation Detail

## JSON → C# Property Name Mapping

`System.Text.Json` uses `JsonPropertyNameAttribute` to map JSON keys to C# property names. The JSON files use camelCase keys.

### core-rules.json Top-Level Keys
| JSON Key | C# Property | Notes |
|----------|-------------|-------|
| `name` | `Name` | |
| `closeCombatToHit` | `CloseCombatToHit` | |
| `toWound` | `ToWound` | |
| `shootingToHit` | `ShootingToHit` | |
| `armourSaves` | `ArmourSaves` | |
| `weapons` | `Weapons` | |
| `armour` | `Armour` | `Dictionary<string, ArmourEntry>` |
| `commonSpecialRules` | `CommonSpecialRules` | `Dictionary<string, SpecialRuleEntry>` |
| `troopTypes` | `TroopTypes` | `Dictionary<string, TroopTypeEntry>` — prose only, unread by the engine |

Use `JsonSerializerOptions { PropertyNameCaseInsensitive = true }` globally — this handles standard camelCase→PascalCase without per-property attributes. Only add explicit `[JsonPropertyName]` where the JSON key doesn't follow the convention.

### Dictionary Keys (Weapon/Armour/Rule Identifiers)
These are the JSON object keys used to identify items. They are **not** renamed by deserialization — they stay as camelCase strings in the dictionary.

| Type | Key Examples | C# Access |
|------|-------------|-----------|
| Close combat weapons | `handWeapon`, `spear`, `greatWeapon`, `polearm`, `lance`, `morningstar`, `flail`, `buckler`, `additionalHandWeapon` | `weapons.CloseCombat["spear"]` |
| Missile weapons | `bow`, `crossbow`, `warbow`, `greatbow`, `shortbow`, `longbow`, `javelin`, `sling`, `throwingWeapon`, `blowpipe`, `pistol`, `handgun`, `brace` | `weapons.Missile["bow"]` |
| Armour | `lightArmour`, `mediumArmour`, `heavyArmour`, `shields`, `buckler`, `barding` | `armour["heavyArmour"]` |
| Special rules | `killingBlow`, `armourPiercing`, `alwaysStrikesFirst`, etc. | `commonSpecialRules["killingBlow"]` |

## NullableIntConverter — Full Implementation

Handles three cases in profile stats: integers, `"*"` (variable/random → null), and string-encoded numbers.

```csharp
public class NullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetInt32();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (str == "*" || string.IsNullOrWhiteSpace(str))
                    return null;
                if (int.TryParse(str, out var val))
                    return val;
                return null;
            case JsonTokenType.Null:
                return null;
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
```

Register on every stat field in `ProfileEntry`:
```csharp
public record ProfileEntry
{
    public string Name { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? M { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? WS { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? BS { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? S { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? T { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? W { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? I { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? A { get; init; }
    [JsonConverter(typeof(NullableIntConverter))] public int? Ld { get; init; }
    public int? Points { get; init; }
}
```

## Field Parsing Functions

### Weapon Strength Parsing

Weapon `strength` field has three formats:

| JSON Value | Meaning | Effective Strength |
|-----------|---------|-------------------|
| `"As user"` | Use model's S stat | `profile.S` |
| `"+1"` | Add to model's S | `profile.S + 1` |
| `"3"`, `"4"`, `"5"`, `"6"` | Fixed strength (missile weapons only) | Use value directly |

```csharp
public static int ParseWeaponStrength(string strengthField, int userStrength)
{
    if (string.Equals(strengthField, "As user", StringComparison.OrdinalIgnoreCase))
        return userStrength;
    if (strengthField.StartsWith("+") && int.TryParse(strengthField, out var mod))
        return userStrength + mod;
    if (int.TryParse(strengthField, out var fixedVal))
        return fixedVal;
    return userStrength; // fallback
}
```

### Armour Save Parsing

Armour `combatSave` / `missileSave` fields use format `"+N/M+"`:

| JSON Value | Save Modifier | Standalone Save |
|-----------|--------------|-----------------|
| `"+1/6+"` | +1 to save | 6+ alone |
| `"+2/5+"` | +2 to save | 5+ alone |
| `"+3/4+"` | +3 to save | 4+ alone |

The modifier is the stacking value (how much this armour piece contributes when combined with others). The standalone save is for when this is the only armour.

```csharp
public static (int modifier, int standaloneSave) ParseArmourSave(string saveField)
{
    // Format: "+N/M+"
    var match = Regex.Match(saveField, @"\+(\d)/(\d)\+");
    if (match.Success)
    {
        int modifier = int.Parse(match.Groups[1].Value);
        int standalone = int.Parse(match.Groups[2].Value);
        return (modifier, standalone);
    }
    return (0, 7); // no save
}
```

Computing combined armour save: sum all modifiers, convert to save value. Base save = 7 - totalModifier. E.g., heavy armour (+3) + shield (+1) = 7 - 4 = 3+.

### Equipment Text → Weapon/Armour Keys

The `equipment` field is comma-separated text. Must normalize to dictionary keys.

**Normalization table** (equipment text → JSON key):
| Equipment Text | JSON Key | Type |
|---------------|----------|------|
| `hand weapon` | `handWeapon` | weapon |
| `additional hand weapon` | `additionalHandWeapon` | weapon |
| `great weapon` | `greatWeapon` | weapon |
| `spear` / `spears` | `spear` | weapon |
| `polearm` / `polearms` | `polearm` | weapon |
| `lance` / `heavy lance` | `lance` | weapon |
| `morning star` | `morningstar` | weapon |
| `flail` / `flails` | `flail` | weapon |
| `bow` / `bows` | `bow` | weapon |
| `crossbow` / `crossbows` | `crossbow` | weapon |
| `javelin` / `javelins` | `javelin` | weapon |
| `shortbow` / `short bow` | `shortbow` | weapon |
| `longbow` / `long bow` | `longbow` | weapon |
| `light armour` | `lightArmour` | armour |
| `medium armour` | `mediumArmour` | armour |
| `heavy armour` | `heavyArmour` | armour |
| `shield` / `shields` | `shields` | armour |
| `buckler` / `bucklers` | `buckler` | armour |
| `barding` | `barding` | armour |

```csharp
public static class EquipmentParser
{
    private static readonly Dictionary<string, (string Key, bool IsWeapon)> NormMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hand weapon"] = ("handWeapon", true),
        ["additional hand weapon"] = ("additionalHandWeapon", true),
        ["great weapon"] = ("greatWeapon", true),
        ["spear"] = ("spear", true),
        ["spears"] = ("spear", true),
        ["polearm"] = ("polearm", true),
        ["polearms"] = ("polearm", true),
        ["lance"] = ("lance", true),
        ["heavy lance"] = ("lance", true),
        ["flail"] = ("flail", true),
        ["flails"] = ("flail", true),
        ["morning star"] = ("morningstar", true),
        ["bow"] = ("bow", true),
        ["bows"] = ("bow", true),
        ["crossbow"] = ("crossbow", true),
        ["crossbows"] = ("crossbow", true),
        ["javelin"] = ("javelin", true),
        ["javelins"] = ("javelin", true),
        ["light armour"] = ("lightArmour", false),
        ["medium armour"] = ("mediumArmour", false),
        ["heavy armour"] = ("heavyArmour", false),
        ["shield"] = ("shields", false),
        ["shields"] = ("shields", false),
        ["buckler"] = ("buckler", false),
        ["bucklers"] = ("buckler", false),
        ["barding"] = ("barding", false),
    };

    public static (List<string> WeaponKeys, List<string> ArmourKeys) Parse(string? equipment)
    {
        var weapons = new List<string>();
        var armour = new List<string>();
        if (string.IsNullOrWhiteSpace(equipment)) return (weapons, armour);

        // Stop parsing at \n — anything after is extended description/magic items
        var mainLine = equipment.Split('\n')[0];

        foreach (var item in mainLine.Split(','))
        {
            var trimmed = item.Trim().TrimEnd('.');
            if (NormMap.TryGetValue(trimmed, out var mapped))
            {
                if (mapped.IsWeapon)
                    weapons.Add(mapped.Key);
                else
                    armour.Add(mapped.Key);
            }
            // else: unrecognized equipment, skip (or log)
        }

        return (weapons, armour);
    }
}
```

**Critical edge case**: Some equipment fields contain full magic item descriptions after `\n` (e.g., Bretonnia named characters). Always split on `\n` and parse only the first line.

### Troop Type Parsing

Format: `"BaseType"` or `"BaseType (Flag1, Flag2)"`. May have trailing period. May have typos.

```csharp
public static TroopTypeInfo Parse(string troopTypeString)
{
    if (string.IsNullOrWhiteSpace(troopTypeString))
        return DefaultInfantry();

    var cleaned = troopTypeString.TrimEnd('.', ' ');
    var match = Regex.Match(cleaned, @"^(.+?)(?:\s*\((.+)\))?$");

    var baseTypeStr = match.Groups[1].Value.Trim();
    var flags = match.Groups[2].Success
        ? match.Groups[2].Value.Split(',').Select(f => f.Trim()).ToList()
        : new List<string>();

    // Normalize typos
    baseTypeStr = baseTypeStr switch
    {
        "Mosntrous Beast" => "Monstrous Beast",    // ogre-kingdoms typo
        "Monstrous Creature" => "Monstrous Beast",  // alternate name
        _ => baseTypeStr
    };

    var baseType = ParseBaseType(baseTypeStr);
    bool isCharacter = flags.Any(f =>
        f.Contains("Character", StringComparison.OrdinalIgnoreCase) ||
        f.Contains("Special Character", StringComparison.OrdinalIgnoreCase));

    // Intrinsic armour save from "(Armour Save 4+)" in flags
    int? intrinsicArmour = null;
    var armourFlag = flags.FirstOrDefault(f => f.StartsWith("Armour Save", StringComparison.OrdinalIgnoreCase));
    if (armourFlag != null)
    {
        var saveMatch = Regex.Match(armourFlag, @"(\d)\+");
        if (saveMatch.Success)
            intrinsicArmour = int.Parse(saveMatch.Groups[1].Value);
    }

    return new TroopTypeInfo { BaseType = baseType, IsCharacter = isCharacter, ... };
}
```

**Troop type → combat properties**:
| Base Type | Min Rank Width | Max Supporting Attacks | Unit Strength | Has Split Profile |
|-----------|---------------|----------------------|---------------|-------------------|
| Infantry | 5 | 1 | 1 | No |
| Large Infantry | 5 | 1 | 1 | No |
| Monstrous Infantry | 3 | 2 | 3 | No |
| Cavalry | 5 | 1 | 2 | Yes |
| Monstrous Cavalry | 3 | 2 | 4 | Yes |
| War Beasts | 5 | 1 | 1 | No |
| Monstrous Beasts | 3 | 2 | 3 | No |
| Swarms | 5 | 0 | 3 | No |
| Monster | 1 | 0 | special | No |
| Chariot | 1 | 0 | special | No |
| War Machine | 1 | 0 | 3 | No |

### Base Size Parsing

Format: `"WxH"`, `"W1xH1 or W2xH2"`, or may contain `\n` followed by unrelated text.

```csharp
public static int ParseBaseWidthMm(string? baseSize)
{
    if (string.IsNullOrWhiteSpace(baseSize)) return 25; // default

    // Take only first line (rest may be mount descriptions or magic items)
    var firstLine = baseSize.Split('\n')[0];

    // Match first WxH pattern
    var match = Regex.Match(firstLine, @"(\d+)x(\d+)");
    if (match.Success)
        return int.Parse(match.Groups[1].Value);

    return 25; // default
}
```

**Real edge cases**:
- `"40x40 or 50x50 (Orion), 25x50 (Hound of Orion)\nMAGIC ITEMS:\n..."` — use first match: 40
- `"25x50 or 30x60\nSTEED OF KURNOUS"` — use first match: 25
- `null` — default to 25mm

### Special Rules Parsing

The most complex parser. Input is a comma-separated string that may contain newlines, parenthetical values, and qualified targets.

**Strategy**: Parse the full string (don't strip newlines first). Use regex to extract known combat-relevant rules. Collect all rule names for display.

```csharp
public static ParsedSpecialRules Parse(string? specialRulesText)
{
    if (string.IsNullOrWhiteSpace(specialRulesText))
        return new ParsedSpecialRules { AllRuleNames = new() };

    var text = specialRulesText;
    var rules = new ParsedSpecialRules();
    var allNames = new List<string>();

    // Split by comma, but respect parentheses — use regex to find rule names
    // Pattern: "RuleName" or "RuleName (Value)"
    var rulePattern = new Regex(
        @"(?:^|,)\s*([A-Z][^,\n]*?)(?:\s*\(([^)]+)\))?\s*(?=,|$|\n)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    foreach (Match match in rulePattern.Matches(text))
    {
        var name = match.Groups[1].Value.Trim();
        var param = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
        allNames.Add(param != null ? $"{name} ({param})" : name);

        // Map known rules to flags (case-insensitive)
        switch (name.ToLowerInvariant())
        {
            case "always strikes first": rules = rules with { AlwaysStrikesFirst = true }; break;
            case "always strikes last": rules = rules with { AlwaysStrikesLast = true }; break;
            case "armour piercing": rules = rules with { ArmourPiercing = ParseIntParam(param) }; break;
            case "devastating charge": rules = rules with { DevastatingCharge = true }; break;
            case "ethereal": rules = rules with { Ethereal = true }; break;
            case "fight in extra ranks": rules = rules with { FightInExtraRanks = ParseIntParam(param) }; break;
            case "flaming attacks": rules = rules with { FlamingAttacks = true }; break;
            case "flammable": rules = rules with { Flammable = true }; break;
            case "frenzy": rules = rules with { Frenzy = true }; break;
            case "hatred": rules = rules with { Hatred = true, HatredTarget = param }; break;
            case "killing blow": rules = rules with { KillingBlow = true }; break;
            case "heroic killing blow": rules = rules with { HeroicKillingBlow = true }; break;
            case "ice attacks": rules = rules with { IceAttacks = true }; break;
            case "ignores armour saves": rules = rules with { IgnoresArmourSaves = true }; break;
            case "impact hits": rules = rules with { ImpactHits = ParseIntParam(param), ImpactHitsRoll = ParseDiceParam(param) }; break;
            case "lightning attacks": rules = rules with { LightningAttacks = true }; break;
            case "magical attacks": rules = rules with { MagicalAttacks = true }; break;
            case "mighty blow": rules = rules with { MightyBlow = ParseIntParam(param) }; break;
            case "multiple wounds": rules = rules with { MultipleWounds = ParseIntParam(param), MultipleWoundsRoll = ParseDiceParam(param) }; break;
            case "natural armour": rules = rules with { NaturalArmour = ParseSaveParam(param) }; break;
            case "parry": rules = rules with { Parry = ParseSaveParam(param) }; break;
            case "poisoned attacks": rules = rules with { PoisonedAttacks = true }; break;
            case "regeneration": rules = rules with { Regeneration = ParseSaveParam(param) }; break;
            case "ward save": rules = rules with { WardSave = ParseSaveParam(param) }; break;
            case "stomp": rules = rules with { Stomp = ParseIntParam(param), StompRoll = ParseDiceParam(param) }; break;
            case "stubborn": rules = rules with { Stubborn = true }; break;
            case "unbreakable": rules = rules with { Unbreakable = true }; break;
            case "unstable": rules = rules with { Unstable = true }; break;
            case "fear": rules = rules with { Fear = true }; break;
            case "terror": rules = rules with { Terror = true }; break;
            case "random attacks": rules = rules with { RandomAttacks = ParseIntParam(param), RandomAttacksRoll = ParseDiceParam(param) }; break;
        }
    }

    return rules with { AllRuleNames = allNames };
}

// "1" → 1, "2" → 2, "D6" → null (not a fixed int)
static int? ParseIntParam(string? param)
{
    if (param != null && int.TryParse(param.TrimEnd('+'), out var val)) return val;
    return null;
}

// "D6" → "D6", "D3+1" → "D3+1", "2" → null (not a dice expression)
static string? ParseDiceParam(string? param)
{
    if (param != null && param.Contains('D', StringComparison.OrdinalIgnoreCase)) return param;
    return null;
}

// "4+" → 4, "6+" → 6
static int? ParseSaveParam(string? param)
{
    if (param == null) return null;
    var m = Regex.Match(param, @"(\d)\+");
    return m.Success ? int.Parse(m.Groups[1].Value) : null;
}
```

**Known parsing pitfalls in real data**:
- `"Killing Blow, Natural Armour (5+)\nHARPIES"` — the `\nHARPIES` is the next unit's header leaking in from PDF extraction. The regex should not treat `HARPIES` as a rule name. Solution: all-caps words after `\n` are likely headers, not rules. Stop parsing at `\n` followed by all-caps.
- `"Blessing of the Lady, Magic Resistance (1)\nSERGEANT-AT-ARMS"` — same issue.
- `"Asrai Archery, Blessings of the Ancients, Fast Cavalry, Forest Strider, Poisoned Attacks,\nWard save (4+)\n• Deepwood Coven: ..."` — Ward save wraps to next line. The comma before `\n` indicates continuation. Solution: if a line ends with `,`, join with next line before parsing.
- `"Ward save (4+)"` — lowercase "save". Matching must be case-insensitive.

**Recommended pre-processing** before regex parsing:
```csharp
static string PreprocessSpecialRules(string text)
{
    // 1. Join lines where previous line ends with comma (wrapped text)
    text = Regex.Replace(text, @",\s*\n\s*", ", ");

    // 2. Truncate at \n followed by all-caps word or bullet (next unit header or description)
    var cutoff = Regex.Match(text, @"\n(?=[A-Z]{3,}|•)");
    if (cutoff.Success)
        text = text[..cutoff.Index];

    return text;
}
```

## Profile Resolution — Multi-Profile Units

Units may have 1, 2, or 3+ profiles. Rules for determining which profile is which:

### Identifying Primary vs Mount Profile
1. **Primary profile**: First profile where `Points != null` (has a per-model cost)
2. **Mount profile**: Second profile (no points cost). Uses mount's M for unit movement and mount's WS/S/I/A for mount attacks.
3. **Crew profile**: Second profile on war machines/monsters where `M == null` and `T == null` and `W == null`. Crew stats (WS, BS, S, I, A, Ld) are used for crew attacks; the vehicle's T/W/Save are used for the model as a whole.

### Edge Cases
| Scenario | Example | Profiles | Resolution |
|----------|---------|----------|------------|
| Single profile | Saurus Warriors | 1 | Use as-is |
| Cavalry (rider + mount) | Knight of the Realm | 2 (Knight, Warhorse) | Rider fights with rider stats, mount gets separate attacks |
| Monster with crew | Stegadon | 2 (Stegadon, Skink Crew) | Crew profile has null M/T/W — use vehicle body for durability |
| Unit with mount options | Wild Rider | 3 (Rider, Elven Steed, Steed of Kurnous) | Profiles 2 and 3 are alternate mounts; user or `mount` field determines which |
| Named character + hound | Orion | 2 (Orion, Hound of Orion) | Hound is an attendant, not a mount — has its own points=null |

```csharp
public static (ProfileEntry Primary, ProfileEntry? Mount) ResolveProfiles(UnitEntry unit)
{
    var profiles = unit.Profiles;
    if (profiles.Count == 0) throw new InvalidDataException($"Unit {unit.Name} has no profiles");
    if (profiles.Count == 1) return (profiles[0], null);

    // Primary = first with Points
    var primary = profiles.FirstOrDefault(p => p.Points.HasValue) ?? profiles[0];

    // Mount/secondary = first non-primary profile
    var mount = profiles.FirstOrDefault(p => p != primary);

    return (primary, mount);
}
```

If there are 3+ profiles (Wild Rider), the mount profile depends on which mount the unit has. The `mount` field on the unit entry (e.g., `"Elven Steed (Animal)"`) identifies the default mount. Match against profile names to select the correct mount profile.

## Handling Null/Missing Fields

| Field | When Null/Missing | Default Behaviour |
|-------|------------------|-------------------|
| `equipment` | null | Unit has no equipment (hand weapon is always implicit per WAP rules) |
| `specialRules` | null or empty | No special rules apply |
| `baseSize` | null | Default 25mm width |
| `options` | null | No optional upgrades available |
| `unitSize` | null | Typically `"1"` for characters |
| `mount` | null | Unit is not mounted |
| `crew` | null | Unit has no crew |
| `magic` | null | Not a wizard |
| `upgrades` | null | No additional upgrade text |
| `notes` | null | No extra rules text |
| Profile stat (M, WS, etc.) | null or `"*"` | Variable/random — display as `"*"`, combat use depends on rule (e.g., Random Movement, Random Attacks) |

## JSON Deserialization Configuration

```csharp
public static JsonSerializerOptions CreateOptions()
{
    return new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

No `[JsonPropertyName]` attributes needed on most properties — `PropertyNameCaseInsensitive = true` handles the camelCase→PascalCase mapping. The `NullableIntConverter` is applied per-field on `ProfileEntry` stats.

## Data Integrity Considerations

### Known Extraction Artefacts
These patterns exist in the real data and must be handled:

1. **Newline pollution**: `specialRules`, `equipment`, `baseSize`, and `upgrades` fields may contain `\n` followed by unrelated content from PDF column extraction.
2. **Troop type typos**: `"Mosntrous Beast"` in ogre-kingdoms.json. Normalize during parsing.
3. **Trailing periods**: `"Monstrous Infantry (Special Character, Forest Spirit)."` — strip trailing `.` before parsing.
4. **Crew field dual use**: Sometimes structured (`"5 Skinks (Lizardman, Skink)"`) and sometimes free text (Maneater's long paragraph). For iteration 1, the crew field is informational only — not parsed for combat.
5. **Mount field format**: `"Warhorse (Animal)"` — mount name followed by race tag in parentheses. The mount name should match a profile name in the profiles array.
6. **Category values**: Mostly `"Lords"`, `"Heroes"`, `"Core"`, `"Special"`, `"Rare"`, but also `"Named Characters"` for some factions.
7. **Weapon-specific notes in specialRules on weapons**: Weapon entries have `specialRules` text like `"+1 Initiative, Fight in Extra Ranks (1), Mighty Blow (1)*"` — the `*` indicates "first round of combat only". Parse the rules but note the asterisk modifier.
