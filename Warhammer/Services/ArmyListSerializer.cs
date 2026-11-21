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
        Composition = list.Overrides.IsEmpty ? null : list.Overrides,
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
        SelectedMultiUpgrades = u.SelectedMultiUpgrades.Count == 0
            ? null
            : u.SelectedMultiUpgrades.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
        CommandUpgrades = u.SelectedCommandUpgrades.Count == 0
            ? null
            : u.SelectedCommandUpgrades.Select(p => new CommandUpgradeDto
            {
                OptionIndex = p.OptionIndex,
                SubOptionIndex = p.SubOptionIndex,
                ChoiceIndex = p.ChoiceIndex,
            }).ToList(),
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
        // A pick of n copies is its name n times, so the saved schema stays a list of names.
        SelectedFactionUpgrades = u.Loadout.SelectedFactionUpgrades
            .ToDictionary(kv => kv.Key, kv => kv.Value
                .SelectMany(p => Enumerable.Repeat(p.Name, Math.Max(1, p.Copies)))
                .ToList()),
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

        await _repo.EnsureLoadedAsync();
        if (!_repo.AllFactions.Contains(faction))
            return new ImportResult(null, warnings,
                [$"Faction '{dto.Faction}' is not in the current system '{_system.CurrentSystem}'."]);

        var factionUnits = await _repo.GetUnitsAsync(faction);
        // A mount and a unit can share a name (High Elves' Great Eagle), and exported data may
        // repeat an entry outright. A list names a unit it holds, so a non-mount entry wins.
        var unitsByName = factionUnits
            .GroupBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(u => u.Category != UnitCategory.CharacterMounts) ?? g.First(),
                StringComparer.OrdinalIgnoreCase);
        var upgradeCosts = await LoadFactionUpgradeCostsAsync(faction);

        var list = new ArmyList
        {
            Faction = faction,
            PointsTarget = dto.PointsTarget,
            Name = dto.Name,
            Overrides = dto.Composition ?? new CompositionOverrides(),
        };

        foreach (var entry in dto.Units)
        {
            if (!unitsByName.TryGetValue(entry.UnitName, out var unit))
            {
                warnings.Add($"Unit '{entry.UnitName}' not found in faction '{faction.ToDisplayName()}' — skipped.");
                continue;
            }

            var cfg = ApplyUnitDto(unit, entry, upgradeCosts, warnings);
            list.Units.Add(cfg);
        }

        return new ImportResult(list, warnings, errors);
    }

    /// <summary>
    /// Upgrade points text per category, keyed by upgrade name. Saved lists store only the names,
    /// so costs are re-resolved from faction data on import — per unit, since a tiered price
    /// depends on the troop type taking it.
    /// </summary>
    private async Task<Dictionary<string, Dictionary<string, string>>> LoadFactionUpgradeCostsAsync(FactionKey faction)
    {
        var costs = new Dictionary<string, Dictionary<string, string>>();
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
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
                byName[entry.Name] = entry.Points;
            costs[category] = byName;
        }

        return costs;
    }

    private static UnitConfiguration ApplyUnitDto(
        Unit unit,
        ArmyListUnitDto dto,
        Dictionary<string, Dictionary<string, string>> upgradeCosts,
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

        if (!string.IsNullOrEmpty(dto.SelectedWeapon))
            cfg.SelectedWeaponKey = dto.SelectedWeapon;

        foreach (var (k, v) in dto.SelectedUpgrades)
            cfg.SelectedUpgrades[k] = v;

        foreach (var (k, v) in dto.SelectedMultiUpgrades ?? new())
            cfg.SelectedMultiUpgrades[k] = v.ToList();

        // Copied as saved: TakenCommandUpgrades skips any pick the book no longer offers.
        foreach (var p in dto.CommandUpgrades ?? [])
            cfg.SelectedCommandUpgrades.Add(new CommandUpgradePick(p.OptionIndex, p.SubOptionIndex, p.ChoiceIndex));

        foreach (var (category, names) in dto.SelectedFactionUpgrades)
        {
            upgradeCosts.TryGetValue(category, out var byName);
            var picks = new List<SelectedFactionUpgrade>();
            foreach (var name in names)
            {
                var text = byName is not null && byName.TryGetValue(name, out var t) ? t : null;
                if (UpgradePrice.TryParseCumulative(text, out var perCopy))
                {
                    // A cumulative rune saved n times is one pick of n copies; later copies were counted with the first.
                    if (picks.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                    var copies = names.Count(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                    if (copies > perCopy.Count)
                    {
                        warnings.Add($"Unit '{cfg.DisplayName}' may take at most {perCopy.Count} of '{name}' — kept {perCopy.Count}, dropped {copies - perCopy.Count}.");
                        copies = perCopy.Count;
                    }
                    picks.Add(new SelectedFactionUpgrade(name, UpgradePrice.CumulativeTotal(text, copies), false, copies));
                    continue;
                }

                // An unknown name, or one this troop type has no tier for, imports free.
                var price = text is not null ? cfg.ResolveUpgradePrice(text) ?? default : default;
                picks.Add(new SelectedFactionUpgrade(name, price.Points, price.PerModel));
            }

            // A hand-edited or older file can hold more picks than the book allows; a rune's copies are one pick.
            if (cfg.MaxPicks(category) is { } max && picks.Count > max)
            {
                var kept = string.Join(", ", picks.Take(max).Select(p => $"'{p.Name}'"));
                var dropped = string.Join(", ", picks.Skip(max).Select(p => $"'{p.Name}'"));
                warnings.Add($"Unit '{cfg.DisplayName}' may take only {(max == 1 ? "one" : max.ToString())} {category.Replace('_', ' ')} — kept {kept}, dropped {dropped}.");
                picks = picks.Take(max).ToList();
            }
            cfg.Loadout.SelectedFactionUpgrades[category] = picks;
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
