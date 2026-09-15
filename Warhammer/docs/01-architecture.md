# Architecture & Project Structure

> **Historical design document.** Written before implementation and kept for intent. Since
> 2026-09-15 the app reads one bundle per game system (formatVersion 2, exported from
> `warhammer-armies-revamped`) instead of per-faction files, factions are ids rather than an enum,
> and options are typed groups addressed by id. `AGENTS.md`, `02-data-format.md` and
> `docs/data-models.md` describe the current shape; trust the code over the specifics below.

## Overview

Fresh Blazor WebAssembly (.NET 10) application. Clean rewrite — no code carried over from the previous version, though proven patterns (Monte Carlo simulation, service-based data loading) inform the design.

## Solution Structure

```
Warhammer/
├── Warhammer.sln
├── Warhammer/                    # Main Blazor WASM project
│   ├── Program.cs                # Entry point, DI registration
│   ├── App.razor                 # Root component, router
│   ├── _Imports.razor            # Global usings
│   ├── Warhammer.csproj
│   │
│   ├── Models/                   # Data models (JSON deserialization + domain)
│   ├── Services/                 # Data loading, rule lookup, equipment parsing
│   ├── Calculator/               # Combat simulation engine
│   ├── Components/               # Reusable Blazor components
│   ├── Pages/                    # Routable page components
│   ├── Layout/                   # Layout components (MainLayout, NavMenu)
│   │
│   ├── wwwroot/
│   │   ├── index.html            # Host page (Bootstrap CDN)
│   │   ├── css/app.css           # Custom styles
│   │   └── data/
│   │       ├── core-rules.json   # Combat tables, weapons, armour, special rules
│   │       └── factions/
│   │           ├── lizardmen.json          # Unit data
│   │           ├── lizardmen-extras.json   # Army rules, items, lores, upgrades
│   │           └── ...                     # 15 factions × 2 files
│   │
│   └── docs/                     # Specification documents (this folder)
│
└── Warhammer.Tests/              # xUnit test project
    ├── Calculator/               # Combat simulation tests
    ├── Services/                 # Service tests
    └── Models/                   # Model/parsing tests
```

## Layer Diagram

```
┌─────────────────────────────────────────────┐
│                   Pages                      │
│         (Matchup, future pages)              │
├─────────────────────────────────────────────┤
│                Components                    │
│  (UnitSelector, UnitConfigurator, Results)   │
├──────────────────┬──────────────────────────┤
│    Calculator     │        Models            │
│  (CombatEngine,  │  (UnitConfig, Profile,   │
│   DiceRoller)    │   SpecialRules, etc.)    │
├──────────────────┴──────────────────────────┤
│                  Services                    │
│  (CoreRulesService, FactionDataService,      │
│   EquipmentParser)                           │
├─────────────────────────────────────────────┤
│              JSON Data Files                 │
│  (core-rules.json, faction/*.json)           │
└─────────────────────────────────────────────┘
```

**Data flows upward**: JSON → Services (load/cache) → Models (deserialize) → Calculator (simulate) → Components (display) → Pages (compose).

**User input flows downward**: Pages → Components (selections) → Models (configuration) → Calculator (parameters).

## Key Design Decisions

### Data Loading
- `HttpClient` fetches JSON from `wwwroot/data/` at runtime (standard Blazor WASM pattern)
- Services cache loaded data (load once, serve from memory)
- Faction data loaded on-demand (not all 15 factions at startup)
- Core rules loaded eagerly (always needed)

### Models
- **Deserialization models**: Mirror JSON structure exactly. Used for `System.Text.Json` deserialization. Immutable after load.
- **Domain models**: Wrap deserialization models with computed properties and user configuration (model count, weapon choice, etc.)
- Keep these two concerns separate — don't mix JSON attributes with business logic in the same class.

### Calculator
- **Stateless**: Takes fully configured units as input, returns results. No internal state.
- **Monte Carlo**: Run N simulations (default 10,000), aggregate results.
- **Special rules**: Applied as modifiers during the combat resolution sequence, not as separate pre/post processing where possible.

### Services
- **CoreRulesService**: Loads core-rules.json. Provides weapon/armour/special rule lookups. Table lookups for to-hit, to-wound, armour save modifiers.
- **FactionDataService**: Loads faction unit and extras JSON on demand. Provides faction list, unit list, magic items, army rules.
- **EquipmentParser**: Parses equipment text fields into weapon/armour identifiers that can be looked up via CoreRulesService.

### Testing
- xUnit project (`Warhammer.Tests`)
- Focus on Calculator: combat outcomes for known inputs should match expected probability distributions
- Test special rule interactions (Flaming + Regeneration, KB + Ward Save, etc.)
- Test service parsing (equipment strings, special rule text)
- No UI/component tests in iteration 1

## Dependency Injection

Registered in `Program.cs`:
- `HttpClient` (built-in for Blazor WASM)
- `CoreRulesService` (singleton — loaded once)
- `FactionDataService` (singleton — caches per-faction)
- Calculator services as needed (likely transient/static methods)

## Data Pipeline

```
WAP PDFs → Python extractors → Extracted/json/*.json → (copy) → wwwroot/data/ → Blazor app
```

The copy step is manual (or scripted). Extractors are not part of the .NET project. The app only reads JSON at runtime.

---

# Pass 2 — Implementation Detail

## File-by-File Breakdown

### Models/

#### `Models/Json/CoreRulesData.cs` — Core Rules Deserialization
```csharp
// Mirrors core-rules.json structure exactly
public record CoreRulesData
{
    public string Name { get; init; }
    public CombatTable CloseCombatToHit { get; init; }
    public CombatTable ToWound { get; init; }
    public ShootingTable ShootingToHit { get; init; }
    public ArmourSaveData ArmourSaves { get; init; }
    public WeaponsData Weapons { get; init; }
    public Dictionary<string, ArmourEntry> Armour { get; init; }
    public Dictionary<string, SpecialRuleEntry> CommonSpecialRules { get; init; }
    public Dictionary<string, TroopTypeEntry> TroopTypes { get; init; }
}

public record CombatTable
{
    public string Description { get; init; }
    public int[][] Table { get; init; }       // [row][col], 0-indexed
    public TableAxes TableAxes { get; init; }
}

public record ShootingTable
{
    public string Description { get; init; }
    public Dictionary<string, int> Table { get; init; }  // "BS1"->6, etc.
}

public record ArmourSaveData
{
    public string Description { get; init; }
    public Dictionary<string, int> StrengthModifiers { get; init; }  // "S4"->-1, etc.
}

public record WeaponsData
{
    public Dictionary<string, WeaponEntry> CloseCombat { get; init; }
    public Dictionary<string, WeaponEntry> Missile { get; init; }
}

public record WeaponEntry
{
    public string Name { get; init; }
    public string Range { get; init; }
    public string Strength { get; init; }      // "As user", "+1", "3", etc.
    public string? SpecialRules { get; init; }
    public string? Notes { get; init; }
}

public record ArmourEntry
{
    public string Name { get; init; }
    public string CombatSave { get; init; }    // "+1/6+", "+3/4+", etc.
    public string MissileSave { get; init; }
    public string? SpecialRules { get; init; }
    public string? Notes { get; init; }
}

public record SpecialRuleEntry
{
    public string Name { get; init; }
    public bool Parameterized { get; init; }
    public string Description { get; init; }
}

public record TroopTypeEntry
{
    public string Name { get; init; }
    public bool Parameterized { get; init; }
    public string Description { get; init; }
}
```

#### `Models/Json/FactionData.cs` — Faction Unit Deserialization
```csharp
public record FactionData
{
    public string FactionName { get; init; }
    public List<UnitEntry> Units { get; init; }
}

public record UnitEntry
{
    public string Name { get; init; }
    public string Category { get; init; }       // Lords, Heroes, Core, Special, Rare
    public List<ProfileEntry> Profiles { get; init; }
    public string TroopType { get; init; }
    public string? BaseSize { get; init; }
    public string? Equipment { get; init; }
    public List<string>? Options { get; init; }
    public string? SpecialRules { get; init; }
    public string? UnitSize { get; init; }
    public int? PointsCost { get; init; }
    public string? Magic { get; init; }
    public string? Upgrades { get; init; }
    public string? Notes { get; init; }
}

public record ProfileEntry
{
    public string Name { get; init; }
    [JsonConverter(typeof(NullableIntConverter))]
    public int? M { get; init; }
    [JsonConverter(typeof(NullableIntConverter))]
    public int? WS { get; init; }
    // ... same for BS, S, T, W, I, A, Ld
    public int? Points { get; init; }
}

// Handles "*" -> null and string numbers -> int
public class NullableIntConverter : JsonConverter<int?> { ... }
```

#### `Models/Json/FactionExtrasData.cs` — Faction Extras Deserialization
```csharp
public record FactionExtrasData
{
    public string FactionName { get; init; }
    public List<ArmyRuleEntry> ArmySpecialRules { get; init; }
    public List<MagicItemEntry> MagicItems { get; init; }
    public List<LoreEntry> LoresOfMagic { get; init; }
    public List<UpgradeEntry> FactionUpgrades { get; init; }
}

public record ArmyRuleEntry
{
    public string Name { get; init; }
    public string Description { get; init; }
}

public record MagicItemEntry
{
    public string Name { get; init; }
    public string Points { get; init; }          // string in JSON
    public string Category { get; init; }
    public string Description { get; init; }
    public string? Restriction { get; init; }
}

public record LoreEntry
{
    public string Name { get; init; }
    public string? Attribute { get; init; }
    public List<SpellEntry> Spells { get; init; }
}

public record SpellEntry
{
    public string Name { get; init; }
    public int Level { get; init; }
    public int CastValue { get; init; }
    public string Description { get; init; }
    public string? Type { get; init; }
}

public record UpgradeEntry
{
    public string Name { get; init; }
    public string Points { get; init; }
    public string Description { get; init; }
}
```

#### `Models/Domain/UnitConfiguration.cs` — User-Configured Unit
```csharp
// Wraps a UnitEntry + user choices, computes effective stats
public class UnitConfiguration
{
    // Source data
    public UnitEntry Unit { get; }
    public ProfileEntry PrimaryProfile { get; }
    public ProfileEntry? MountProfile { get; }

    // User configuration
    public int ModelCount { get; set; }
    public int Frontage { get; set; }           // models wide
    public string SelectedWeaponKey { get; set; }   // key into closeCombat weapons
    public bool HasChampion { get; set; }
    public bool HasMusician { get; set; }
    public bool HasStandardBearer { get; set; }

    // Parsed data (computed on construction/change)
    public TroopTypeInfo TroopType { get; }     // parsed from string
    public int BaseWidthMm { get; }             // parsed from baseSize
    public ParsedSpecialRules SpecialRules { get; }  // parsed from specialRules string
    public List<string> EquipmentWeapons { get; }    // parsed weapon keys
    public List<string> EquipmentArmour { get; }     // parsed armour keys

    // Computed stats (depend on weapon choice, opponent, charge state)
    public int EffectiveStrength(WeaponEntry? weapon, bool isCharging);
    public int EffectiveArmourSave(int attackerStrength, int attackerAP);
    public int Ranks { get; }                   // ModelCount / Frontage
    public int RankBonus { get; }               // min(Ranks - 1, 3), if frontage >= min width
    public int ModelsInBaseContact(UnitConfiguration opponent);
    public int TotalAttacks(UnitConfiguration opponent, bool isCharging);
}
```

#### `Models/Domain/TroopTypeInfo.cs` — Parsed Troop Type
```csharp
public enum BaseTroopType
{
    Infantry, LargeInfantry, MonstrousInfantry,
    Cavalry, MonstrousCavalry,
    WarBeasts, MonstrousBeasts,
    Swarms, Monster, Chariot, WarMachine, Shrine
}

public record TroopTypeInfo
{
    public BaseTroopType BaseType { get; init; }
    public bool IsCharacter { get; init; }
    public int? IntrinsicArmourSave { get; init; }  // from "(Armour Save 4+)"
    public int MinRankWidth { get; init; }           // 5 for infantry, 3 for monstrous, etc.
    public int MaxSupportingAttacks { get; init; }   // 1 for infantry, 2 for monstrous, etc.
    public int UnitStrength { get; init; }           // 1 for infantry, 2 for cavalry, etc.
    public bool HasSplitProfile { get; init; }       // cavalry/monstrous cavalry
    public List<string> InherentSpecialRules { get; init; }  // Fear, Impact Hits(1), etc.

    public static TroopTypeInfo Parse(string troopTypeString);
}
```

#### `Models/Domain/ParsedSpecialRules.cs` — Parsed Rule Flags
```csharp
// Extracted combat-relevant flags from the specialRules text field
public record ParsedSpecialRules
{
    public bool AlwaysStrikesFirst { get; init; }
    public bool AlwaysStrikesLast { get; init; }
    public int? ArmourPiercing { get; init; }       // AP(X)
    public bool DevastatingCharge { get; init; }
    public bool Ethereal { get; init; }
    public int? FightInExtraRanks { get; init; }
    public bool FlamingAttacks { get; init; }
    public bool Flammable { get; init; }
    public bool Frenzy { get; init; }
    public bool Hatred { get; init; }
    public string? HatredTarget { get; init; }      // "Vampire Counts", etc.
    public bool IceAttacks { get; init; }
    public bool IgnoresArmourSaves { get; init; }
    public int? ImpactHits { get; init; }           // null, or fixed count
    public string? ImpactHitsRoll { get; init; }    // "D6", "D3+1" etc.
    public bool KillingBlow { get; init; }
    public bool HeroicKillingBlow { get; init; }
    public bool LightningAttacks { get; init; }
    public bool MagicalAttacks { get; init; }
    public int? MightyBlow { get; init; }
    public int? MultipleWounds { get; init; }
    public string? MultipleWoundsRoll { get; init; }
    public int? NaturalArmour { get; init; }        // save value, e.g. 5 for (5+)
    public int? Parry { get; init; }                // ward value, e.g. 6 for (6+)
    public bool PoisonedAttacks { get; init; }
    public int? Regeneration { get; init; }         // save value
    public int? WardSave { get; init; }             // save value
    public int? Stomp { get; init; }
    public string? StompRoll { get; init; }
    public bool Stubborn { get; init; }
    public bool Unbreakable { get; init; }
    public bool Unstable { get; init; }
    public bool Fear { get; init; }
    public bool Terror { get; init; }
    public int? RandomAttacks { get; init; }        // null if not random
    public string? RandomAttacksRoll { get; init; } // "D6", "D3+1"

    // Raw text for display (unparsed rules shown as badges)
    public List<string> AllRuleNames { get; init; }

    public static ParsedSpecialRules Parse(string? specialRulesText);
}
```

### Services/

#### `Services/CoreRulesService.cs`
```csharp
public class CoreRulesService
{
    private CoreRulesData? _data;
    private readonly HttpClient _http;

    public async Task EnsureLoadedAsync();  // idempotent load

    // Table lookups
    public int GetToHitScore(int attackerWS, int defenderWS);   // clamp 1-10
    public int GetToWoundScore(int strength, int toughness);     // clamp 1-10
    public int GetArmourSaveModifier(int strength);              // returns negative value

    // Weapon/armour lookups
    public WeaponEntry? GetCloseCombatWeapon(string key);
    public WeaponEntry? GetMissileWeapon(string key);
    public ArmourEntry? GetArmour(string key);
    public WeaponEntry GetHandWeapon();   // fallback, always available

    // Special rules
    public SpecialRuleEntry? GetSpecialRule(string key);
    public IReadOnlyDictionary<string, SpecialRuleEntry> AllSpecialRules { get; }
}
```

#### `Services/FactionDataService.cs`
```csharp
public class FactionDataService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, FactionData> _unitCache = new();
    private readonly Dictionary<string, FactionExtrasData> _extrasCache = new();

    // Faction list (hardcoded or from index file)
    public IReadOnlyList<string> FactionNames { get; }

    // On-demand loading
    public async Task<FactionData> GetFactionUnitsAsync(string factionKey);
    public async Task<FactionExtrasData> GetFactionExtrasAsync(string factionKey);
}
```

#### `Services/EquipmentParser.cs`
```csharp
public static class EquipmentParser
{
    // "Hand weapon, light armour, shield" -> (["handWeapon"], ["lightArmour", "shields"])
    public static (List<string> WeaponKeys, List<string> ArmourKeys) Parse(string? equipment);
}
```

### Calculator/

#### `Calculator/CombatEngine.cs` — Main Simulation Entry Point
```csharp
public class CombatEngine
{
    private readonly CoreRulesService _coreRules;
    private readonly Random _rng;

    public CombatSimulationResult Simulate(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,       // who charged, round number
        int iterations = 10_000
    );
}
```

#### `Calculator/CombatContext.cs`
```csharp
public record CombatContext
{
    public ChargeState Charge { get; init; }     // AttackerCharged, DefenderCharged, Neither
    public int CombatRound { get; init; } = 1;   // for first-round effects (Hatred, Mighty Blow)
}

public enum ChargeState { Neither, AttackerCharged, DefenderCharged }
```

#### `Calculator/CombatResolver.cs` — Single-Round Resolution
```csharp
public class CombatResolver
{
    private readonly CoreRulesService _coreRules;

    // Resolve one full round, returns both sides' results
    public RoundResult ResolveRound(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        CombatContext context,
        Random rng
    );
}
```

#### `Calculator/AttackResolver.cs` — Resolves One Side's Attacks
```csharp
public class AttackResolver
{
    // Resolve all attacks from one unit against the other
    public AttackResult ResolveAttacks(
        UnitConfiguration attacker,
        UnitConfiguration defender,
        WeaponEntry weapon,
        CombatContext context,
        bool isMountAttack,
        Random rng
    );
}
```

#### `Calculator/Models/CombatSimulationResult.cs`
```csharp
public record CombatSimulationResult
{
    public int Iterations { get; init; }
    public double AttackerWinPct { get; init; }
    public double DefenderWinPct { get; init; }
    public double DrawPct { get; init; }
    public double AvgAttackerWounds { get; init; }
    public double AvgDefenderWounds { get; init; }
    public double AvgAttackerCombatRes { get; init; }
    public double AvgDefenderCombatRes { get; init; }
    public CombatResBreakdown AvgAttackerBreakdown { get; init; }
    public CombatResBreakdown AvgDefenderBreakdown { get; init; }
    public List<DistributionEntry> Distribution { get; init; }  // CR diff histogram
}

public record CombatResBreakdown
{
    public double Wounds { get; init; }
    public double RankBonus { get; init; }
    public double StandardBearer { get; init; }
    public double ChargeBonus { get; init; }
    public double FearBonus { get; init; }
}

public record DistributionEntry
{
    public int CombatResDifference { get; init; }   // positive = attacker won
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public record RoundResult
{
    public int AttackerWounds { get; init; }
    public int DefenderWounds { get; init; }
    public int AttackerCombatRes { get; init; }
    public int DefenderCombatRes { get; init; }
    public CombatResBreakdown AttackerBreakdown { get; init; }
    public CombatResBreakdown DefenderBreakdown { get; init; }
}

public record AttackResult
{
    public int TotalHits { get; init; }
    public int TotalWounds { get; init; }
    public int KillingBlowWounds { get; init; }
    public int WardSaved { get; init; }
    public int UnsavedWounds { get; init; }
}
```

## DI Registration (`Program.cs`)

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<CoreRulesService>();
builder.Services.AddSingleton<FactionDataService>();
builder.Services.AddTransient<CombatEngine>();

await builder.Build().RunAsync();
```

## Implementation Order (Iteration 1)

Build bottom-up so each layer can be tested independently:

1. **JSON deserialization models** (`Models/Json/`) — just records matching JSON
2. **NullableIntConverter** — handle `"*"` in stat profiles
3. **CoreRulesService** — load + cache core-rules.json, table lookups
4. **FactionDataService** — load + cache faction JSON on demand
5. **TroopTypeInfo.Parse()** — parse troop type strings
6. **ParsedSpecialRules.Parse()** — regex-based special rule parser
7. **EquipmentParser** — parse equipment text to weapon/armour keys
8. **UnitConfiguration** — wrap unit + user choices, compute effective stats
9. **AttackResolver** — resolve one side's attacks (to-hit → to-wound → saves)
10. **CombatResolver** — full round resolution (both sides, combat resolution)
11. **CombatEngine** — Monte Carlo wrapper around CombatResolver
12. **Components** — UnitSelector, UnitConfigurator, ResultsPanel
13. **MatchupPage** — wire everything together

Steps 1-11 are testable without UI. Steps 12-13 are the UI layer.
