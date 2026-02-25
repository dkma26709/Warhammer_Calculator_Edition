using Microsoft.AspNetCore.Components;
using Warhammer.Calculator;
using Warhammer.Models;
using Warhammer.Services;

namespace Warhammer.Pages;

public class MatchupBase : ComponentBase
{
    [Inject] protected CoreRulesService CoreRules { get; set; } = default!;

    protected ExtractedUnit? _attackerUnit;
    protected ExtractedUnit? _defenderUnit;
    protected string _attackerFaction = "";
    protected string _defenderFaction = "";
    protected UnitConfiguration? _attackerConfig;
    protected UnitConfiguration? _defenderConfig;

    protected int _simulationCount = 10000;
    protected bool _attackerCharged;
    protected bool _defenderCharged;
    protected bool _isSimulating;

    protected CombatSimulationSummary? _summary;
    protected CombatCalculator? _calculator;

    protected bool CanSimulate => _attackerConfig is not null && _defenderConfig is not null && !_isSimulating;

    protected string AttackerName => _attackerConfig?.SourceUnit.Name ?? "Attacker";
    protected string DefenderName => _defenderConfig?.SourceUnit.Name ?? "Defender";

    protected void OnAttackerSelected((ExtractedUnit unit, string factionName) selection)
    {
        _attackerUnit = selection.unit;
        _attackerFaction = selection.factionName;
        _attackerConfig = null;
        _summary = null;
    }

    protected void OnDefenderSelected((ExtractedUnit unit, string factionName) selection)
    {
        _defenderUnit = selection.unit;
        _defenderFaction = selection.factionName;
        _defenderConfig = null;
        _summary = null;
    }

    protected void OnAttackerConfigChanged(UnitConfiguration config)
    {
        _attackerConfig = config;
    }

    protected void OnDefenderConfigChanged(UnitConfiguration config)
    {
        _defenderConfig = config;
    }

    protected override async Task OnInitializedAsync()
    {
        await CoreRules.LoadAsync();
        _calculator = new CombatCalculator(CoreRules);
    }

    protected async Task RunSimulation()
    {
        if (_attackerConfig is null || _defenderConfig is null || _calculator is null) return;

        _isSimulating = true;
        _summary = null;
        StateHasChanged();

        await Task.Delay(1);

        var results = _calculator.SimulateMultiple(
            _attackerConfig, _defenderConfig, _simulationCount,
            _attackerCharged, _defenderCharged);
        _summary = _calculator.Summarize(results);

        _isSimulating = false;
    }
}
