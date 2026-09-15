# Project Structure

> See `AGENTS.md` at the repo root for the layout with commentary; this is the short form.

## Repository Layout

```
<repo root>\
├── Warhammer/                      # Main Blazor WASM app
│   ├── Program.cs                  # Entry point, service registration
│   ├── App.razor                   # Router configuration
│   │
│   ├── Models/
│   │   ├── Json/                   # JSON deserialisation models (records, no logic)
│   │   │   ├── BundleData.cs       # The bundle: rulebook, composition, raw factions
│   │   │   ├── CompositionData.cs  # Percentages, duplicate chart, unit constraints
│   │   │   ├── FactionData.cs      # One army book: rules, items, upgrades, lores, units
│   │   │   ├── UnitEntry.cs        # A unit and its profiles
│   │   │   ├── OptionGroup.cs      # Typed option groups and choices
│   │   │   ├── CoreRulesData.cs    # core-rules.json mapping
│   │   │   ├── TroopTypesData.cs   # troop-types.json mapping
│   │   │   └── Converters.cs       # Lenient readers for fields the bundle writes in several shapes
│   │   │
│   │   ├── Domain/                 # Business logic models
│   │   │   ├── Faction.cs, Unit.cs, UnitConfiguration.cs, OptionSelections.cs
│   │   │   ├── CompositionRules.cs, FactionUpgrades.cs, MagicItemAllowance.cs
│   │   │   ├── ArmyList.cs, ArmyListDto.cs, ArmyLoadout.cs, RosterDocument.cs
│   │   │   ├── ParsedSpecialRules.cs, ParsedWeaponRules.cs, TroopTypeInfo.cs
│   │   │   └── GameSystem.cs, MagicItemCategories.cs, UnitModifier.cs, ...
│   │   │
│   │   └── Enums/                  # OptionEnums (OptionKind, SelectMode, CommandRole),
│   │                               #   UnitCategory, CloseCombatWeaponKey, ArmourKey, ...
│   │
│   ├── Services/
│   │   ├── GameSystemService.cs    # Active system key; data paths
│   │   ├── BundleService.cs        # Fetches the bundle; factions on demand; troop types
│   │   ├── CoreRulesService.cs     # Loads core-rules.json; chart lookups
│   │   ├── GameDataRepository.cs   # Facade: resolves Units, composition rules, lores
│   │   ├── ArmyListValidator.cs, ArmyListSerializer.cs, ArmyRosterBuilder.cs
│   │   ├── EquipmentParser.cs      # Equipment text -> weapon/armour keys
│   │   └── PortraitService.cs
│   │
│   ├── Calculator/                 # Combat engine (pure C#)
│   ├── Components/                 # Razor components
│   ├── Pages/                      # Matchup, Gauntlet, Builder, Roster
│   ├── Layout/MainLayout.razor
│   └── wwwroot/
│       ├── index.html
│       ├── data/war/
│       │   ├── war.json            # The bundle (generated in warhammer-armies-revamped)
│       │   ├── core-rules.json     # Engine charts, weapons, armour (hand-maintained)
│       │   └── troop-types.json    # Troop type figures (hand-maintained)
│       ├── data/portrait-manifest.json
│       └── images/units/{faction-id}/
│
├── Warhammer.Tests/                # xUnit, one class per subject
└── tools/PortraitFetcher/          # Fetches portraits and writes the manifest
```

## Service Registration (Program.cs)

```csharp
builder.Services.AddScoped<GameSystemService>();
builder.Services.AddScoped<CoreRulesService>();
builder.Services.AddScoped<BundleService>();
builder.Services.AddScoped<IGameDataRepository, GameDataRepository>();
builder.Services.AddTransient<CombatEngine>();
builder.Services.AddSingleton<ArmyListValidator>();
builder.Services.AddScoped<ArmyListSerializer>();
builder.Services.AddScoped<ArmyRosterBuilder>();
builder.Services.AddScoped<PortraitService>();
```

Note: in Blazor WASM there is a single DI scope for the app's lifetime, so Scoped and Singleton behave identically here — data services are effectively app-wide caches either way.
