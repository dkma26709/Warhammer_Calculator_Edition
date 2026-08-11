# Project Structure

> **Note:** this document describes the layout as of the initial Matchup-only release and has not been fully updated since. The repo has since gained the Gauntlet (`Pages/Gauntlet.razor`) and Builder (`Pages/Builder.razor`) pages, army builder services (`ArmyListValidator`, `ArmyListSerializer`), the `IGameDataRepository`/`GameDataRepository` facade, `PortraitService` + `tools/PortraitFetcher`, multi-round combat (`BreakTestResolver`, `FleeAndPursueResolver`), and many more components and tests. See `AGENTS.md` at the repo root for the current layout.

## Repository Layout

```
<repo root>\
├── Warhammer/                      # Main Blazor WASM app
│   ├── Program.cs                  # Entry point, service registration
│   ├── App.razor                   # Router configuration
│   ├── _Imports.razor              # Global usings
│   │
│   ├── Models/
│   │   ├── Json/                   # JSON deserialisation models
│   │   │   ├── CoreRulesData.cs    # core-rules.json mapping
│   │   │   ├── FactionData.cs      # {faction}.json mapping (UnitEntry, ProfileEntry)
│   │   │   ├── FactionExtrasData.cs# {faction}-extras.json mapping
│   │   │   ├── NullableIntConverter.cs  # Handles null/"*"/"-" stat values
│   │   │   └── OptionsListConverter.cs  # Handles both string[] and object[] options
│   │   │
│   │   └── Domain/                 # Business logic models
│   │       ├── UnitConfiguration.cs     # Unit + user config + computed combat stats
│   │       ├── ParsedSpecialRules.cs    # 32 typed special rule properties
│   │       ├── ParsedWeaponRules.cs     # Weapon-specific modifiers
│   │       └── TroopTypeInfo.cs         # Troop type enum + combat properties
│   │
│   ├── Services/
│   │   ├── CoreRulesService.cs     # Loads core-rules.json; table lookups
│   │   ├── FactionDataService.cs   # Loads faction JSONs; caching
│   │   └── EquipmentParser.cs      # Equipment text -> weapon/armour keys
│   │
│   ├── Calculator/
│   │   ├── CombatEngine.cs         # Monte Carlo orchestrator (10k iterations)
│   │   ├── CombatResolver.cs       # Single-round phase-based resolution
│   │   ├── AttackResolver.cs       # To-hit, to-wound, saves, special rules
│   │   ├── BaseContactCalculator.cs # Geometric base contact calculation
│   │   └── Models/
│   │       ├── CombatContext.cs     # Charge state, round number
│   │       └── CombatResults.cs    # Result records (RoundResult, AttackResult, etc.)
│   │
│   ├── Components/
│   │   ├── UnitSelector.razor      # Faction + category + unit dropdowns
│   │   ├── UnitConfigurator.razor   # Stats, model count, weapon, command group
│   │   ├── UnitPanel.razor          # Card wrapper: selector + configurator
│   │   ├── CombatControls.razor     # Charge state + simulate button
│   │   ├── EffectiveStatsPanel.razor# Computed stats comparison table
│   │   ├── ResultsPanel.razor       # Full results: win%, wounds, CR, distributions
│   │   ├── StatTable.razor          # Profile stat row display
│   │   └── SpecialRuleBadges.razor  # Colour-coded rule badges
│   │
│   ├── Pages/
│   │   └── Matchup.razor           # Main page (routes: /, /matchup)
│   │
│   ├── Layout/
│   │   └── MainLayout.razor        # Root layout template
│   │
│   └── wwwroot/
│       ├── index.html              # Bootstrap CDN, app entry
│       └── data/
│           ├── core-rules.json     # Combat tables, weapons, armour, rules
│           └── factions/
│               ├── {faction}.json       # Unit rosters (15 files)
│               └── {faction}-extras.json # Army rules, items, spells (15 files)
│
└── Warhammer.Tests/                # MSTest project
    ├── AttackResolverTests.cs      # Attack resolution + special rules
    ├── CombatResolverTests.cs      # Full round resolution + phases
    ├── CoreRulesServiceTests.cs    # Table lookups + parsing
    ├── EquipmentParserTests.cs     # Equipment string mapping
    ├── FactionDeserializationTests.cs # All 15 factions deserialise correctly
    ├── SpecialRulesParsingTests.cs # 32 special rules parsing
    ├── TroopTypeTests.cs           # Troop type classification
    ├── UnitConfigurationTests.cs   # Unit config calculations
    └── WeaponRulesTests.cs         # Weapon special rule parsing
```

## Service Registration (Program.cs)

```csharp
builder.Services.AddScoped<CoreRulesService>();
builder.Services.AddScoped<FactionDataService>();
builder.Services.AddScoped<IGameDataRepository, GameDataRepository>();
builder.Services.AddTransient<CombatEngine>();
builder.Services.AddSingleton<ArmyListValidator>();
builder.Services.AddScoped<ArmyListSerializer>();
builder.Services.AddScoped<ArmyRosterBuilder>();
builder.Services.AddScoped<PortraitService>();
```

Note: in Blazor WASM there is a single DI scope for the app's lifetime, so Scoped and Singleton behave identically here — data services are effectively app-wide caches either way.
