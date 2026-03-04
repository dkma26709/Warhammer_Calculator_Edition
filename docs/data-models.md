# Data Models

## JSON Data Files

### core-rules.json

WAP 9th Edition core combat rules. Loaded by `CoreRulesService`.

```
closeCombatToHit.table    — 10x10 int[][] (WS 1-10 vs WS 1-10)
toWound.table             — 10x10 int[][] (S 1-10 vs T 1-10)
armourSaves.strengthModifiers — {"S3": 0, "S4": -1, "S5": -2, ...}
weapons.closeCombat       — {key: WeaponEntry} (handWeapon, spear, greatWeapon, etc.)
weapons.missile           — {key: WeaponEntry} (bow, crossbow, etc.)
armour                    — {key: ArmourEntry} (lightArmour, heavyArmour, shield, etc.)
commonSpecialRules        — {key: SpecialRuleEntry} (killing-blow, hatred, etc.)
troopTypes                — {key: TroopTypeEntry}
```

### {faction}.json

Unit roster per army. Loaded by `FactionDataService`.

```json
{
  "factionName": "Lizardmen",
  "units": [{
    "name": "Saurus Warriors",
    "category": "Core",
    "profiles": [
      {"name": "Saurus Warrior", "M": 4, "WS": 3, "BS": 0, "S": 4, "T": 4, "W": 1, "I": 1, "A": 2, "Ld": 8, "points": 13}
    ],
    "troopType": "Infantry (Lizardman, Saurus)",
    "baseSize": "25x25 or 30x30",
    "equipment": "Hand weapon, shield",
    "options": [
      {"type": "equipment", "action": "add", "choices": [{"name": "spear", "points": 1, "perModel": true}], "raw": "May take spears +1 point/model"},
      {"type": "command", "role": "Leader", "points": 5, "raw": "May upgrade one Saurus Warrior to a Leader +5 points"},
      {"type": "command", "role": "Musician", "points": 5, "raw": "May upgrade one Saurus Warrior to a Musician +5 points"},
      {"type": "command", "role": "Standard Bearer", "points": 10, "raw": "May upgrade one Saurus Warrior to a Standard Bearer +10 points"}
    ],
    "specialRules": "Cold-blooded, Predatory Fighter, Natural Armour (5+)",
    "unitSize": "10+",
    "pointsCost": 13
  }]
}
```

**Multi-profile units** (e.g. cavalry, characters on mounts) have multiple entries in `profiles`. The primary profile has `points`; the mount profile does not.

**Options** can be either simple strings or structured objects with `type`, `raw`, `choices` fields. The `OptionsListConverter` normalises both to `List<string>`.

### {faction}-extras.json

Army rules, magic items, lores. Currently loaded but not used in combat simulation.

---

## C# Model Mapping

### JSON -> C# (Models/Json/)

| JSON File | C# Record | Key Fields |
|-----------|-----------|------------|
| core-rules.json | `CoreRulesData` | `CloseCombatToHit`, `ToWound`, `ArmourSaves`, `Weapons`, `Armour` |
| {faction}.json | `FactionData` | `FactionName`, `Units: List<UnitEntry>` |
| — | `UnitEntry` | `Name`, `Category`, `Profiles`, `TroopType`, `Equipment`, `Options`, `SpecialRules` |
| — | `ProfileEntry` | `Name`, `M/WS/BS/S/T/W/I/A/Ld` (nullable int), `Points` |
| {faction}-extras.json | `FactionExtrasData` | `ArmySpecialRules`, `MagicItems`, `LoresOfMagic`, `FactionUpgrades` |

### Custom JSON Converters

| Converter | Purpose |
|-----------|---------|
| `NullableIntConverter` | Parses stat values: numbers, `"*"`, `"-"`, whitespace -> `int?` |
| `OptionsListConverter` | Normalises `string[]` or `object[]` options to `List<string>` |

### Domain Models (Models/Domain/)

| Model | Built From | Purpose |
|-------|-----------|---------|
| `UnitConfiguration` | `UnitEntry` | User config + computed combat stats (attacks, strength, armour save, ranks, points) |
| `ParsedSpecialRules` | `UnitEntry.SpecialRules` string | 32 typed properties for combat rules |
| `ParsedWeaponRules` | `WeaponEntry.SpecialRules` + `Notes` | Weapon modifiers (initiative, extra attacks, MB, FiER, parry) |
| `TroopTypeInfo` | `UnitEntry.TroopType` string | Base type enum + combat properties (rank width, support attacks, unit strength) |

### ParsedSpecialRules — All 32 Properties

**Offensive:** KillingBlow, HeroicKillingBlow, PoisonedAttacks, FlamingAttacks, IceAttacks, LightningAttacks, MagicalAttacks, IgnoresArmourSaves, ArmourPiercing(int), Hatred, Frenzy, DevastatingCharge, MightyBlow(int), AlwaysStrikesFirst, AlwaysStrikesLast, MultipleWounds(int), MultipleWoundsRoll(string), FightInExtraRanks(int)

**Defensive:** NaturalArmour(int), WardSave(int), Regeneration(int), Parry(int), Ethereal, Flammable

**Impact/Stomp:** ImpactHits(int), ImpactHitsRoll(string), Stomp(int), StompRoll(string), RandomAttacks(int), RandomAttacksRoll(string)

**Morale:** Fear, Terror, Stubborn, Unbreakable, Unstable, ColdBlooded

### TroopTypeInfo — Key Properties by Type

| Base Type | Min Rank Width | Max Support Attacks | Unit Strength | Split Profile |
|-----------|---------------|--------------------|----|------|
| Infantry | 5 | 1 | 1 | No |
| Large Infantry | 3 | 2 | 1 | No |
| Monstrous Infantry | 3 | 2 | 2 | No |
| Cavalry | 5 | 1 | 2 | Yes |
| Monstrous Cavalry | 3 | 2 | 3 | Yes |
| War Beasts | 5 | 1 | 1 | No |
| Monstrous Beasts | 3 | 2 | 2 | No |
| Monster | 1 | 0 | 3 | No |
| Chariot | 1 | 0 | 3 | No |
| Swarms | 3 | 5 | 3 | No |
