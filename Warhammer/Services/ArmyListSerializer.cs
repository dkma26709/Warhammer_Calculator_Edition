using System.Text.Json;
using System.Text.Json.Serialization;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ArmyListSerializer(IGameDataRepository repo) => _repo = repo;

    // ─── Export ──────────────────────────────────────────────────

    public string Serialize(ArmyList list)
    {
        var dto = ToDto(list);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public ArmyListDto ToDto(ArmyList list) => new()
    {
        Schema = ArmyListDto.CurrentSchema,
        Faction = list.Faction.ToJsonKey(),
        PointsTarget = list.PointsTarget,
        Name = list.Name,
        Units = list.Units.Select(UnitToDto).ToList(),
    };

    private static ArmyListUnitDto UnitToDto(UnitConfiguration u) => new()
    {
        UnitName = u.ResolvedUnit.Name,
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
        Banner = u.SelectedBanner == null ? null : new MagicItemDto
        {
            Name = u.SelectedBanner.Name,
            Category = u.SelectedBanner.Category,
            PointsCost = u.SelectedBanner.PointsCost,
            Modifier = u.SelectedBanner.Modifier,
        },
        WizardLevel = u.WizardLevel,
        SelectedLore = u.SelectedLore,
        SelectedSpells = u.SelectedSpells.ToList(),
        Modifiers = u.Modifiers.ToList(),
        ShadowDance = u.SelectedShadowDance.ToString(),
        TreeAspect = u.SelectedTreeAspect.ToString(),
        UseTreeWhack = u.UseTreeWhack,
        IsGeneral = u.IsGeneral,
        IsBattleStandardBearer = u.IsBattleStandardBearer,
        SelectedFactionUpgrades = u.SelectedFactionUpgrades
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
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

        if (!FactionKeyExtensions.TryFromJsonKey(dto.Faction, out var faction))
            return new ImportResult(null, warnings, [$"Unknown faction '{dto.Faction}'."]);

        var factionUnits = await _repo.GetUnitsAsync(faction);
        var unitsByName = factionUnits.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);

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

            var cfg = ApplyUnitDto(unit, entry);
            list.Units.Add(cfg);
        }

        return new ImportResult(list, warnings, errors);
    }

    private static UnitConfiguration ApplyUnitDto(Unit unit, ArmyListUnitDto dto)
    {
        var cfg = new UnitConfiguration(unit)
        {
            ModelCount = dto.ModelCount,
            Frontage = dto.Frontage,
            HasChampion = dto.HasChampion,
            HasMusician = dto.HasMusician,
            HasStandardBearer = dto.HasStandardBearer,
            WizardLevel = dto.WizardLevel,
            SelectedLore = dto.SelectedLore,
            UseTreeWhack = dto.UseTreeWhack,
            IsGeneral = dto.IsGeneral,
            IsBattleStandardBearer = dto.IsBattleStandardBearer,
        };

        if (!string.IsNullOrEmpty(dto.SelectedWeapon))
            cfg.SelectedWeaponKey = dto.SelectedWeapon;

        foreach (var (k, v) in dto.SelectedUpgrades)
            cfg.SelectedUpgrades[k] = v;

        foreach (var (category, names) in dto.SelectedFactionUpgrades)
            cfg.SelectedFactionUpgrades[category] = new List<string>(names);

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
            cfg.SelectedBanner = new SelectedMagicItem
            {
                Name = dto.Banner.Name,
                Category = dto.Banner.Category,
                PointsCost = dto.Banner.PointsCost,
                Modifier = dto.Banner.Modifier,
            };
        }

        cfg.SelectedSpells.AddRange(dto.SelectedSpells);
        cfg.Modifiers.AddRange(dto.Modifiers);

        if (Enum.TryParse<ShadowDance>(dto.ShadowDance, out var sd)) cfg.SelectedShadowDance = sd;
        if (Enum.TryParse<TreeAspect>(dto.TreeAspect, out var ta)) cfg.SelectedTreeAspect = ta;

        return cfg;
    }
}
