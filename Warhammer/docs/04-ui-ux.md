# UI/UX Specification

> **Note**: This spec is the most likely to evolve during implementation. Treat it as a starting direction, not a rigid contract.

## Overview

Single-page combat matchup tool. Two units configured side by side, simulation runs on demand, results displayed below. Dark/military theme fitting the Warhammer aesthetic.

## Pages (Iteration 1)

### Combat Matchup (`/` or `/matchup`)
The only page in iteration 1. Two-column layout with attacker on the left, defender on the right, and shared controls/results in the centre or below.

## User Flow

```
1. Select Faction (dropdown)        →  loads faction unit list
2. Select Unit (dropdown)           →  loads unit profiles & stats
3. Configure Unit                   →  model count, frontage, weapon, armour, command group
4. (Repeat for opponent side)
5. Set Combat Context               →  who charged, round number (for first-round effects)
6. Run Simulation (button)          →  Monte Carlo runs, results appear
7. Review Results                   →  win/loss/draw %, wounds, combat resolution breakdown
```

## Component Hierarchy

```
MatchupPage
├── UnitPanel (×2: attacker & defender)
│   ├── UnitSelector
│   │   ├── Faction Dropdown
│   │   ├── Category Filter (optional)
│   │   └── Unit Dropdown
│   ├── UnitConfigurator
│   │   ├── StatTable (profile stats display)
│   │   ├── ModelCount Input
│   │   ├── Frontage Input
│   │   ├── WeaponSelector (dropdown from core rules + unit options)
│   │   ├── ArmourDisplay (from equipment, computed)
│   │   ├── CommandGroup (checkboxes: champion, musician, standard bearer)
│   │   ├── EffectiveStats (computed: effective S, effective save, effective attacks)
│   │   └── SpecialRuleBadges (parsed from specialRules field)
│   └── UnitSummary (compact view of configured unit)
├── CombatControls
│   ├── ChargeToggle (who charged, or neither)
│   ├── SimulateButton
│   └── SimulationCount (default 10,000)
└── ResultsPanel
    ├── OutcomeSummary (win/loss/draw percentages)
    ├── WoundsSummary (average wounds dealt by each side)
    ├── CombatResBreakdown (wounds + rank bonus + standard + charge + fear)
    └── DistributionChart (bar chart of combat result differential)
```

## Key UI Elements

### Stat Table
Display unit profile in the classic Warhammer stat-line format:

```
| Name            | M | WS | BS | S | T | W | I | A | Ld |
|-----------------|---|----|----|---|---|---|---|---|----|
| Saurus Warrior  | 4 |  3 |  0 | 4 | 4 | 1 | 1 | 2 |  8 |
```

If unit has a mount profile, show both rows.

### Effective Stats
When opponent is configured, show computed values:
- **Effective Strength**: Base S + weapon modifier + Mighty Blow (if applicable)
- **Effective Save**: Armour + shield + natural armour - strength modifier - AP
- **Effective Attacks**: Models in base contact × attacks per model + supporting attacks
- **Ward/Regen Save**: Best of ward save and regeneration

### Special Rule Badges
Parsed from the unit's `specialRules` field. Display as small labelled badges/chips. Colour-code by effect type (offensive, defensive, utility). Show parameterized values (e.g., "Natural Armour (4+)").

### Results Display
- **Primary**: Win % / Draw % / Loss % with colour coding (green/grey/red)
- **Wounds**: Average wounds dealt by attacker, average wounds dealt by defender
- **Combat Resolution**: Breakdown showing each component (wounds, rank bonus, standard, charge, fear)
- **Distribution Chart**: Bar chart showing frequency of each combat result differential (-10 to +10 or similar range)

## Combat Controls

### Charge Toggle
Three states: "Attacker charged", "Defender charged", "Neither"
Affects: charge bonus (+1 CR), Devastating Charge, lance/spear rules, Impact Hits, Mighty Blow on weapons

### Simulation Count
Default 10,000. Allow user to adjust (1000, 5000, 10000, 50000) if desired. Higher = more accurate but slower.

## Layout

### Desktop (≥ 768px)
```
┌──────────────────────────────────────────────────┐
│                    Header / Title                 │
├───────────────┬──────────┬───────────────────────┤
│   Attacker    │ Controls │     Defender           │
│   UnitPanel   │  Charge  │     UnitPanel          │
│               │  Button  │                        │
├───────────────┴──────────┴───────────────────────┤
│                  Results Panel                    │
│   Win/Loss/Draw   |   Wounds   |   Distribution  │
└──────────────────────────────────────────────────┘
```

### Mobile (< 768px)
Stack vertically: Attacker → Defender → Controls → Results

## Styling Direction

> Superseded by `07-war-room-redesign.md` (2026-09-29), which also replaces the green/grey/red
> results colouring below.

- Dark theme (dark backgrounds, light text) — fitting Warhammer's grim aesthetic
- Bootstrap 5 (CDN loaded) as the base framework
- Accent colours for faction identity (optional, can be plain initially)
- Minimal custom CSS — rely on Bootstrap utilities where possible
- Tables styled with Bootstrap table classes
- Cards for each unit panel
- Responsive grid (Bootstrap row/col)

## State Management

- All state lives in the page component (MatchupPage)
- Unit configuration passed down as parameters to child components
- Simulation triggered by button click, results stored in page state
- No global state store needed for iteration 1
- Services injected via DI for data loading (not state)

## Data Display Considerations

- Unit names may be long — truncate or wrap gracefully
- Some units have very long special rules text — show badges, tooltip for full description
- Options list can be lengthy — consider collapsible section
- Some stats are null (e.g., BS=0 for melee-only) — display as "-" or "0"
- Multiple profiles (rider + mount) — show both rows clearly labelled

## Future Iterations (Not in Scope)

- Iteration 2: Magic item selector dropdowns (filter by category, show points, restrictions)
- Iteration 3: Round-by-round timeline display, casualty tracking visualization
- Iteration 4: Spell casting probability calculator page, leadership test calculator, charge range calculator
- Army list display / unit browser
- Side-by-side comparison of different configurations

---

# Pass 2 — Implementation Detail

## MatchupPage State

The page component owns all state. Child components receive data via `[Parameter]` and report changes via `EventCallback`.

```csharp
@page "/"
@page "/matchup"

// Injected services
@inject CoreRulesService CoreRules
@inject FactionDataService FactionData
@inject CombatEngine CombatEngine

@code {
    // --- State ---
    // Attacker side
    private string? _attackerFactionKey;
    private FactionData? _attackerFactionData;
    private UnitEntry? _attackerUnit;
    private UnitConfiguration? _attackerConfig;

    // Defender side
    private string? _defenderFactionKey;
    private FactionData? _defenderFactionData;
    private UnitEntry? _defenderUnit;
    private UnitConfiguration? _defenderConfig;

    // Combat context
    private ChargeState _chargeState = ChargeState.Neither;

    // Results
    private CombatSimulationResult? _result;
    private bool _isSimulating;

    // --- Lifecycle ---
    protected override async Task OnInitializedAsync()
    {
        await CoreRules.EnsureLoadedAsync();
    }
}
```

## Component Parameters & Events

### UnitPanel

Container for one side (attacker or defender). Wraps UnitSelector + UnitConfigurator.

```csharp
// Components/UnitPanel.razor
[Parameter] public string Label { get; set; }                    // "Attacker" or "Defender"
[Parameter] public IReadOnlyList<string> FactionNames { get; set; }
[Parameter] public string? SelectedFactionKey { get; set; }
[Parameter] public EventCallback<string> SelectedFactionKeyChanged { get; set; }
[Parameter] public UnitEntry? SelectedUnit { get; set; }
[Parameter] public EventCallback<UnitEntry> SelectedUnitChanged { get; set; }
[Parameter] public FactionData? FactionData { get; set; }
[Parameter] public UnitConfiguration? Configuration { get; set; }
[Parameter] public EventCallback<UnitConfiguration> ConfigurationChanged { get; set; }
[Parameter] public UnitConfiguration? OpponentConfig { get; set; } // for effective stats
```

### UnitSelector

Faction dropdown → Unit dropdown. Optionally filters by category.

```csharp
// Components/UnitSelector.razor
[Parameter] public IReadOnlyList<string> FactionNames { get; set; }
[Parameter] public string? SelectedFactionKey { get; set; }
[Parameter] public EventCallback<string> OnFactionSelected { get; set; }
[Parameter] public FactionData? FactionData { get; set; }
[Parameter] public UnitEntry? SelectedUnit { get; set; }
[Parameter] public EventCallback<UnitEntry> OnUnitSelected { get; set; }

@code {
    private string? _categoryFilter;

    private IEnumerable<UnitEntry> FilteredUnits =>
        FactionData?.Units
            .Where(u => _categoryFilter == null || u.Category == _categoryFilter)
            .OrderBy(u => u.Name) ?? Enumerable.Empty<UnitEntry>();

    private IEnumerable<string> Categories =>
        FactionData?.Units.Select(u => u.Category).Distinct().OrderBy(c => c)
        ?? Enumerable.Empty<string>();
}
```

### UnitConfigurator

Displays stats, model count, weapon choice, command group, effective stats, special rules. Only rendered when a unit is selected.

```csharp
// Components/UnitConfigurator.razor
[Parameter] public UnitEntry Unit { get; set; }
[Parameter] public UnitConfiguration Configuration { get; set; }
[Parameter] public EventCallback<UnitConfiguration> ConfigurationChanged { get; set; }
[Parameter] public UnitConfiguration? OpponentConfig { get; set; }  // null until opponent configured

@code {
    // Track previous unit to detect unit change vs re-render
    private UnitEntry? _previousUnit;

    protected override void OnParametersSet()
    {
        if (_previousUnit != Unit)
        {
            _previousUnit = Unit;
            // Reset configuration to defaults for new unit
        }
    }

    private void OnModelCountChanged(int count)
    {
        Configuration.ModelCount = count;
        ConfigurationChanged.InvokeAsync(Configuration);
    }

    private void OnFrontageChanged(int frontage)
    {
        Configuration.Frontage = frontage;
        ConfigurationChanged.InvokeAsync(Configuration);
    }

    private void OnWeaponChanged(string weaponKey)
    {
        Configuration.SelectedWeaponKey = weaponKey;
        ConfigurationChanged.InvokeAsync(Configuration);
    }
}
```

### StatTable

Displays Warhammer stat-line. Shows mount profile if present.

```csharp
// Components/StatTable.razor
[Parameter] public ProfileEntry Profile { get; set; }
[Parameter] public ProfileEntry? MountProfile { get; set; }
[Parameter] public string UnitName { get; set; }
```

### EffectiveStats

Computed values shown when both units are configured.

```csharp
// Components/EffectiveStats.razor
[Parameter] public UnitConfiguration Config { get; set; }
[Parameter] public UnitConfiguration Opponent { get; set; }
[Parameter] public ChargeState ChargeState { get; set; }
[Parameter] public CoreRulesService CoreRules { get; set; }
```

### SpecialRuleBadges

Displays parsed special rules as labelled badges.

```csharp
// Components/SpecialRuleBadges.razor
[Parameter] public ParsedSpecialRules Rules { get; set; }
[Parameter] public IReadOnlyDictionary<string, SpecialRuleEntry>? RuleDescriptions { get; set; }
```

### CombatControls

Charge toggle, simulate button, iteration count.

```csharp
// Components/CombatControls.razor
[Parameter] public ChargeState ChargeState { get; set; }
[Parameter] public EventCallback<ChargeState> ChargeStateChanged { get; set; }
[Parameter] public bool CanSimulate { get; set; }               // both units configured?
[Parameter] public bool IsSimulating { get; set; }
[Parameter] public EventCallback OnSimulate { get; set; }
```

### ResultsPanel

Displays simulation results. Only rendered when `_result != null`.

```csharp
// Components/ResultsPanel.razor
[Parameter] public CombatSimulationResult Result { get; set; }
[Parameter] public string AttackerName { get; set; }
[Parameter] public string DefenderName { get; set; }
```

## Event Flow

```
User selects faction (UnitSelector dropdown)
  → OnFactionSelected fires
  → UnitPanel calls SelectedFactionKeyChanged
  → MatchupPage loads FactionData async, clears unit selection

User selects unit (UnitSelector dropdown)
  → OnUnitSelected fires
  → UnitPanel calls SelectedUnitChanged
  → MatchupPage creates new UnitConfiguration from unit defaults

User changes model count / weapon / command group (UnitConfigurator inputs)
  → UnitConfigurator updates Configuration, fires ConfigurationChanged
  → MatchupPage receives updated config, clears stale results

User clicks Simulate (CombatControls button)
  → OnSimulate fires
  → MatchupPage sets _isSimulating = true, runs CombatEngine.Simulate() on Task.Run
  → On completion: sets _result, _isSimulating = false, calls StateHasChanged()
```

## Blazor Lifecycle Notes

- **OnInitializedAsync**: Load core rules once. This is the only eagerly loaded data.
- **OnParametersSet** (not Async): Used in UnitConfigurator to detect unit change. Compare `_previousUnit` to avoid infinite render loops.
- **StateHasChanged**: Called automatically after EventCallback. Avoid calling manually except after `Task.Run` (which doesn't capture the sync context).
- **@key directive**: Use `@key="unit.Name"` on UnitConfigurator to force re-creation when unit changes, rather than patching the existing component.

## Simulation Execution

Running Monte Carlo on the UI thread would block rendering. Options:

1. **Task.Run** (simplest): Offload to thread pool. Works in Blazor WASM because .NET 10 has proper threading support. Call `StateHasChanged()` after completion via `InvokeAsync`.
2. **Chunked with yield**: Run N iterations, yield to UI every 1000 iterations for progress display. More complex but provides progress feedback.

For iteration 1, use `Task.Run` with a spinner during simulation.

```csharp
private async Task RunSimulation()
{
    if (_attackerConfig == null || _defenderConfig == null) return;

    _isSimulating = true;
    _result = null;
    StateHasChanged();

    var context = new CombatContext
    {
        Charge = _chargeState,
        CombatRound = 1
    };

    _result = await Task.Run(() =>
        CombatEngine.Simulate(_attackerConfig, _defenderConfig, context));

    _isSimulating = false;
    StateHasChanged();
}
```

## Bootstrap Markup Patterns

### Page Layout (Desktop 3-Column)
```html
<div class="container-fluid">
    <div class="row">
        <div class="col-md-5">
            <UnitPanel Label="Attacker" ... />
        </div>
        <div class="col-md-2 d-flex flex-column align-items-center justify-content-center">
            <CombatControls ... />
        </div>
        <div class="col-md-5">
            <UnitPanel Label="Defender" ... />
        </div>
    </div>
    @if (_result != null)
    {
        <div class="row mt-4">
            <div class="col-12">
                <ResultsPanel ... />
            </div>
        </div>
    }
</div>
```

### Unit Panel Card
```html
<div class="card bg-dark text-light border-secondary">
    <div class="card-header">
        <h5 class="card-title mb-0">@Label</h5>
    </div>
    <div class="card-body">
        <UnitSelector ... />
        @if (SelectedUnit != null)
        {
            <hr class="border-secondary" />
            <UnitConfigurator ... />
        }
    </div>
</div>
```

### Stat Table
```html
<table class="table table-sm table-dark table-bordered mb-2">
    <thead>
        <tr>
            <th>Name</th><th>M</th><th>WS</th><th>BS</th><th>S</th>
            <th>T</th><th>W</th><th>I</th><th>A</th><th>Ld</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>@Profile.Name</td>
            <td>@FormatStat(Profile.M)</td>
            <!-- ... -->
        </tr>
        @if (MountProfile != null)
        {
            <tr class="text-muted">
                <td>@MountProfile.Name</td>
                <!-- ... -->
            </tr>
        }
    </tbody>
</table>

@code {
    private string FormatStat(int? value) => value?.ToString() ?? "*";
}
```

### Special Rule Badges
```html
<div class="d-flex flex-wrap gap-1 mt-2">
    @foreach (var rule in Rules.AllRuleNames)
    {
        <span class="badge @GetBadgeClass(rule)"
              title="@GetRuleDescription(rule)">
            @rule
        </span>
    }
</div>

@code {
    private string GetBadgeClass(string ruleName)
    {
        // Classify by effect type for colour coding
        // Offensive (red): Killing Blow, Hatred, Frenzy, Poisoned Attacks, etc.
        // Defensive (blue): Ward Save, Regeneration, Natural Armour, Parry, etc.
        // Utility (grey): Fear, Terror, Stubborn, Unbreakable, etc.
        return "bg-secondary"; // placeholder — classify during implementation
    }
}
```

### Results — Win/Loss/Draw Bar
```html
<div class="d-flex mb-3" style="height: 2rem;">
    <div class="bg-success d-flex align-items-center justify-content-center text-white"
         style="width: @(Result.AttackerWinPct)%">
        @Result.AttackerWinPct.ToString("F1")%
    </div>
    <div class="bg-secondary d-flex align-items-center justify-content-center text-white"
         style="width: @(Result.DrawPct)%">
        @Result.DrawPct.ToString("F1")%
    </div>
    <div class="bg-danger d-flex align-items-center justify-content-center text-white"
         style="width: @(Result.DefenderWinPct)%">
        @Result.DefenderWinPct.ToString("F1")%
    </div>
</div>
```

### Distribution Chart

For iteration 1, use a simple CSS bar chart (no external chart library dependency). Each bar represents a combat resolution differential value.

```html
<div class="mt-3">
    <h6>Combat Resolution Distribution</h6>
    @foreach (var entry in Result.Distribution.OrderBy(e => e.CombatResDifference))
    {
        <div class="d-flex align-items-center mb-1">
            <span class="text-end me-2" style="width: 2rem;">@entry.CombatResDifference</span>
            <div class="@(entry.CombatResDifference > 0 ? "bg-success" : entry.CombatResDifference < 0 ? "bg-danger" : "bg-secondary")"
                 style="height: 1rem; width: @(entry.Percentage * 3)%;">
            </div>
            <span class="ms-2 small">@entry.Percentage.ToString("F1")%</span>
        </div>
    }
</div>
```

Upgrade to a proper charting library (ApexCharts) in a later iteration if the CSS approach proves insufficient.

## Loading States

| State | Display |
|-------|---------|
| Core rules loading (page init) | Full-page spinner with "Loading game data..." |
| Faction data loading | Spinner in UnitPanel card body |
| Simulation running | Disable simulate button, show spinner in results area |
| No results yet | Results panel hidden |
| Error loading data | Alert message in affected panel |

```html
@if (_isLoading)
{
    <div class="d-flex justify-content-center align-items-center" style="min-height: 200px;">
        <div class="spinner-border text-light" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </div>
}
```

## Dark Theme CSS

Minimal custom CSS in `wwwroot/css/app.css`. Bootstrap 5.3+ has a built-in dark mode via `data-bs-theme="dark"` on `<html>`.

```html
<!-- index.html -->
<html data-bs-theme="dark">
```

Additional custom styles (keep minimal):
```css
/* wwwroot/css/app.css */
body {
    background-color: #1a1a2e;
}

.card {
    border-color: #444;
}

/* Stat table compact sizing */
.table-sm th, .table-sm td {
    padding: 0.2rem 0.4rem;
    text-align: center;
}

.table-sm th:first-child, .table-sm td:first-child {
    text-align: left;
}
```

## Default Values for New Unit Configuration

When a user selects a unit, create a `UnitConfiguration` with sensible defaults:

| Field | Default | Source |
|-------|---------|--------|
| ModelCount | Minimum unit size (parse from `unitSize` field, e.g., `"10+"` → 10) | Unit data |
| Frontage | Min rank width for troop type (5 for infantry, 3 for monstrous) | Troop type rules |
| SelectedWeaponKey | First weapon from equipment | Equipment parser |
| HasChampion | false | |
| HasMusician | false | |
| HasStandardBearer | false | |
