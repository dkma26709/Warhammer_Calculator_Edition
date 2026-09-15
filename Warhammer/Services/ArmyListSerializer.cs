using System.Text.Json;
using System.Text.Json.Serialization;
using Warhammer.Models.Domain;
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
        Faction = list.FactionId,
        PointsTarget = list.PointsTarget,
        Name = list.Name,
        Units = list.Units.Select(UnitToDto).ToList(),
        Composition = list.Overrides.IsEmpty ? null : list.Overrides,
    };

    private static ArmyListUnitDto UnitToDto(UnitConfiguration u) => new()
    {
        UnitId = u.ResolvedUnit.Id,
        UnitName = u.ResolvedUnit.Name,
        ProfileId = u.ResolvedUnit.Profiles.Count > 1 ? u.SelectedProfileId : null,
        CustomName = string.IsNullOrWhiteSpace(u.Loadout.CustomName) ? null : u.Loadout.CustomName.Trim(),
        ModelCount = u.ModelCount,
        Frontage = u.Frontage,
        SelectedWeapon = u.SelectedWeaponKey,
        HasChampion = u.HasChampion,
        HasMusician = u.HasMusician,
        HasStandardBearer = u.HasStandardBearer,
        Selections = u.Selections.ToDictionary(),
        MagicItems = u.SelectedMagicItems.Select(ItemToDto).ToList(),
        Banner = u.Loadout.SelectedBanner == null ? null : ItemToDto(u.Loadout.SelectedBanner),
        WizardLevel = u.Loadout.WizardLevel,
        SelectedLore = u.Loadout.SelectedLore,
        SelectedSpells = u.Loadout.SelectedSpells.ToList(),
        Modifiers = u.Modifiers.ToList(),
        ShadowDance = u.SelectedShadowDance.ToString(),
        TreeAspect = u.SelectedTreeAspect.ToString(),
        UseTreeWhack = u.UseTreeWhack,
        IsGeneral = u.Loadout.IsGeneral,
        IsBattleStandardBearer = u.Loadout.IsBattleStandardBearer,
        FactionUpgrades = u.Loadout.SelectedFactionUpgrades
            .Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Select(p => p.Id).ToList()),
    };

    private static MagicItemDto ItemToDto(SelectedMagicItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Category = item.Category,
        PointsCost = item.PointsCost,
        Modifier = item.Modifier,
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

        if (string.IsNullOrWhiteSpace(dto.Faction))
            return new ImportResult(null, warnings, ["The list names no faction."]);

        var factions = _repo.AllFactions;
        if (factions.Count > 0 && !factions.Any(f => string.Equals(f.Id, dto.Faction, StringComparison.OrdinalIgnoreCase)))
            return new ImportResult(null, warnings, [$"Unknown faction '{dto.Faction}'."]);

        IReadOnlyList<Unit> factionUnits;
        try
        {
            factionUnits = await _repo.GetUnitsAsync(dto.Faction);
        }
        catch (Exception ex)
        {
            return new ImportResult(null, warnings, [$"Unknown faction '{dto.Faction}': {ex.Message}"]);
        }

        var unitsById = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in factionUnits)
            unitsById.TryAdd(unit.Id, unit);

        var upgrades = await LoadUpgradesAsync(dto.Faction);

        var list = new ArmyList
        {
            FactionId = dto.Faction,
            PointsTarget = dto.PointsTarget,
            Name = dto.Name,
            Overrides = dto.Composition ?? new CompositionOverrides(),
        };

        foreach (var entry in dto.Units)
        {
            if (!unitsById.TryGetValue(entry.UnitId ?? "", out var unit))
            {
                // A file from another release may carry an id the book has since renamed; the
                // name is the only other handle there is.
                unit = factionUnits.FirstOrDefault(u =>
                    string.Equals(u.Name, entry.UnitName, StringComparison.OrdinalIgnoreCase));
                if (unit is null)
                {
                    warnings.Add($"Unit '{entry.UnitName}' ({entry.UnitId}) not found in faction " +
                                 $"'{_repo.GetFactionDisplayName(dto.Faction)}' — skipped.");
                    continue;
                }

                warnings.Add($"Unit '{entry.UnitName}' was matched by name; its id '{entry.UnitId}' is not in the data.");
            }

            var cfg = ApplyUnitDto(unit, entry, upgrades, warnings);
            list.Units.Add(cfg);
        }

        return new ImportResult(list, warnings, errors);
    }

    /// <summary>
    /// The faction's upgrade entries by id, and by name for a file that predates ids. Saved
    /// lists store only the ids, so costs are re-resolved from the book on import.
    /// </summary>
    private async Task<IReadOnlyList<UpgradeEntry>> LoadUpgradesAsync(string factionId)
    {
        try
        {
            return (await _repo.GetFactionAsync(factionId)).Upgrades;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static UnitConfiguration ApplyUnitDto(
        Unit unit,
        ArmyListUnitDto dto,
        IReadOnlyList<UpgradeEntry> upgrades,
        List<string> warnings)
    {
        var cfg = new UnitConfiguration(unit)
        {
            // Clamped the way the configurator's own inputs clamp them. An imported file is the
            // one path that bypasses those, and a frontage of 0 makes a 40-model unit report a
            // single rank rather than failing.
            ModelCount = Math.Max(1, dto.ModelCount),
            Frontage = Math.Max(1, dto.Frontage),
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

        if (!string.IsNullOrEmpty(dto.ProfileId))
        {
            cfg.SelectedProfileId = dto.ProfileId;
            if (!string.Equals(cfg.SelectedProfileId, dto.ProfileId, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{unit.Name}: profile '{dto.ProfileId}' is not in the data; the first profile is fielded.");
        }

        if (!string.IsNullOrEmpty(dto.SelectedWeapon))
            cfg.SelectedWeaponKey = dto.SelectedWeapon;

        cfg.Selections.CopyFrom(dto.Selections);

        foreach (var (family, ids) in dto.FactionUpgrades)
        {
            var picks = new List<SelectedFactionUpgrade>();
            foreach (var id in ids)
            {
                var entry = upgrades.FirstOrDefault(u => string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase))
                         ?? upgrades.FirstOrDefault(u => string.Equals(u.Name, id, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    picks.Add(new SelectedFactionUpgrade(id, id, 0));
                    continue;
                }

                var (points, perModel) = FactionUpgrades.ResolveCost(
                    entry.Cost, cfg.TroopType.IsCharacter, cfg.TroopType.BaseType);
                picks.Add(new SelectedFactionUpgrade(entry.Id, entry.Name, perModel ? points * cfg.ModelCount : points));
            }

            cfg.Loadout.SelectedFactionUpgrades[family] = picks;
        }

        foreach (var mi in dto.MagicItems)
        {
            cfg.SelectedMagicItems.Add(new SelectedMagicItem
            {
                Id = mi.Id,
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
                Id = dto.Banner.Id,
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
