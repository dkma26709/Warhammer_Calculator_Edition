using System.Text.Json;
using System.Text.Json.Serialization;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;

namespace Warhammer.Services;

public sealed record ImportResult(
    ArmyList? List,
    List<string> Warnings,
    List<string> Errors)
{
    public bool Success => List != null && Errors.Count == 0;
}

public class ArmyListSerializer
{
    private readonly IGameDataRepository _repo;
    private readonly GameSystemService _system;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ArmyListSerializer(IGameDataRepository repo, GameSystemService system)
    {
        _repo = repo;
        _system = system;
    }

    // ─── Export ──────────────────────────────────────────────────

    public string Serialize(ArmyList list)
    {
        var dto = ToDto(list);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public ArmyListDto ToDto(ArmyList list) => new()
    {
        Schema = ArmyListDto.CurrentSchema,
        System = _system.CurrentSystem,
        Faction = list.Faction.ToJsonKey(),
        PointsTarget = list.PointsTarget,
        Name = list.Name,
        Units = list.Units.Select(UnitToDto).ToList(),
    };

    private static ArmyListUnitDto UnitToDto(UnitConfiguration u) => new()
    {
        UnitName = u.ResolvedUnit.Name,
        CustomName = string.IsNullOrWhiteSpace(u.Loadout.CustomName) ? null : u.Loadout.CustomName.Trim(),
        ModelCount = u.ModelCount,
        Frontage = u.Frontage,
        SelectedWeapon = u.SelectedWeaponKey,
        HasChampion = u.HasChampion,
        HasMusician = u.HasMusician,
        HasStandardBearer = u.HasStandardBearer,
        SelectedUpgrades = new Dictionary<int, int>(u.SelectedUpgrades),
        MagicItems = u.SelectedMagicItems.Select(m => new MagicItemDto
        {
            Name = m.Name,
            Category = m.Category,
            PointsCost = m.PointsCost,
            Modifier = m.Modifier,
        }).ToList(),
        Banner = u.Loadout.SelectedBanner == null ? null : new MagicItemDto
        {
            Name = u.Loadout.SelectedBanner.Name,
            Category = u.Loadout.SelectedBanner.Category,
            PointsCost = u.Loadout.SelectedBanner.PointsCost,
            Modifier = u.Loadout.SelectedBanner.Modifier,
        },
        WizardLevel = u.Loadout.WizardLevel,
        SelectedLore = u.Loadout.SelectedLore,
        SelectedSpells = u.Loadout.SelectedSpells.ToList(),
        Modifiers = u.Modifiers.ToList(),
        ShadowDance = u.SelectedShadowDance.ToString(),
        TreeAspect = u.SelectedTreeAspect.ToString(),
        UseTreeWhack = u.UseTreeWhack,
        IsGeneral = u.Loadout.IsGeneral,
        IsBattleStandardBearer = u.Loadout.IsBattleStandardBearer,
        SelectedFactionUpgrades = u.Loadout.SelectedFactionUpgrades
            .ToDictionary(kv => kv.Key, kv => kv.Value.Select(p => p.Name).ToList()),
    };

    // ─── Import ──────────────────────────────────────────────────

    public async Task<ImportResult> DeserializeAsync(string json)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        ArmyListDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ArmyListDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return new ImportResult(null, warnings, [$"Invalid JSON: {ex.Message}"]);
        }

        if (dto == null)
            return new ImportResult(null, warnings, ["Empty or null payload."]);

        if (dto.Schema != ArmyListDto.CurrentSchema)
            warnings.Add($"Schema '{dto.Schema}' does not match current '{ArmyListDto.CurrentSchema}' — attempting anyway.");

        if (!string.Equals(dto.System, _system.CurrentSystem, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"List was saved for system '{dto.System}' but the current system is '{_system.CurrentSystem}' — attempting anyway.");

        if (!FactionKeyExtensions.TryFromJsonKey(dto.Faction, out var faction))
            return new ImportResult(null, warnings, [$"Unknown faction '{dto.Faction}'."]);

        var factionUnits = await _repo.GetUnitsAsync(faction);
        var unitsByName = factionUnits.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);
        var upgradeCosts = await LoadFactionUpgradeCostsAsync(faction);

        var list = new ArmyList
        {
            Faction = faction,
            PointsTarget = dto.PointsTarget,
            Name = dto.Name,
        };

        foreach (var entry in dto.Units)
        {
            if (!unitsByName.TryGetValue(entry.UnitName, out var unit))
            {
                warnings.Add($"Unit '{entry.UnitName}' not found in faction '{faction.ToDisplayName()}' — skipped.");
                continue;
            }

            var cfg = ApplyUnitDto(unit, entry, upgradeCosts);
            list.Units.Add(cfg);
        }

        return new ImportResult(list, warnings, errors);
    }

    /// <summary>
    /// Upgrade costs per category, keyed by upgrade name. Saved lists store only the names,
    /// so costs are re-resolved from faction data on import.
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, double>>> LoadFactionUpgradeCostsAsync(FactionKey faction)
    {
        var costs = new Dictionary<string, Dictionary<string, double>>();
        FactionExtrasData extras;
        try
        {
            extras = await _repo.GetFactionExtrasAsync(faction);
        }
        catch (Exception)
        {
            return costs;
        }

        foreach (var (category, entries) in extras.FactionUpgrades)
        {
            var byName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
                byName[entry.Name] = double.TryParse(entry.Points, out var points) ? points : 0;
            costs[category] = byName;
        }

        return costs;
    }

    private static UnitConfiguration ApplyUnitDto(
        Unit unit,
        ArmyListUnitDto dto,
        Dictionary<string, Dictionary<string, double>> upgradeCosts)
    {
        var cfg = new UnitConfiguration(unit)
        {
            ModelCount = dto.ModelCount,
            Frontage = dto.Frontage,
            HasChampion = dto.HasChampion,
            HasMusician = dto.HasMusician,
            HasStandardBearer = dto.HasStandardBearer,
            UseTreeWhack = dto.UseTreeWhack,
            Loadout =
            {
                CustomName = dto.CustomName,
                WizardLevel = dto.WizardLevel,
                SelectedLore = dto.SelectedLore,
                IsGeneral = dto.IsGeneral,
                IsBattleStandardBearer = dto.IsBattleStandardBearer,
            },
        };

        if (!string.IsNullOrEmpty(dto.SelectedWeapon))
            cfg.SelectedWeaponKey = dto.SelectedWeapon;

        foreach (var (k, v) in dto.SelectedUpgrades)
            cfg.SelectedUpgrades[k] = v;

        foreach (var (category, names) in dto.SelectedFactionUpgrades)
        {
            upgradeCosts.TryGetValue(category, out var byName);
            cfg.Loadout.SelectedFactionUpgrades[category] = names
                .Select(name => new SelectedFactionUpgrade(
                    name, byName is not null && byName.TryGetValue(name, out var points) ? points : 0))
                .ToList();
        }

        foreach (var mi in dto.MagicItems)
        {
            cfg.SelectedMagicItems.Add(new SelectedMagicItem
            {
                Name = mi.Name,
                Category = mi.Category,
                PointsCost = mi.PointsCost,
                Modifier = mi.Modifier,
            });
        }

        if (dto.Banner != null)
        {
            cfg.Loadout.SelectedBanner = new SelectedMagicItem
            {
                Name = dto.Banner.Name,
                Category = dto.Banner.Category,
                PointsCost = dto.Banner.PointsCost,
                Modifier = dto.Banner.Modifier,
            };
        }

        cfg.Loadout.SelectedSpells.AddRange(dto.SelectedSpells);
        cfg.Modifiers.AddRange(dto.Modifiers);

        if (Enum.TryParse<ShadowDance>(dto.ShadowDance, out var sd)) cfg.SelectedShadowDance = sd;
        if (Enum.TryParse<TreeAspect>(dto.TreeAspect, out var ta)) cfg.SelectedTreeAspect = ta;

        return cfg;
    }
}
