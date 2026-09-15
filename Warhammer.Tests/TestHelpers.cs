using System.Net;
using System.Text;
using System.Text.Json;
using Warhammer.Calculator;
using Warhammer.Calculator.Models;
using Warhammer.Models.Domain;
using Warhammer.Models.Enums;
using Warhammer.Models.Json;
using Warhammer.Services;

namespace Warhammer.Tests;

/// <summary>
/// Shared test infrastructure. Loads core rules once, provides unit factories and statistical helpers.
/// </summary>
public static class TestHelpers
{
    public static readonly CoreRulesData CoreData;
    private static readonly Lazy<GameDataRepository> LazyRepo;

    /// <summary>The faction id fixture units are filed under. Any real faction will do; it only has to exist.</summary>
    public const string FixtureFaction = "empire";

    static TestHelpers()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data", GameSystems.Default);
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
        LazyRepo = new Lazy<GameDataRepository>(() => CreateRepository());
    }

    /// <summary>Shared repository instance for test unit resolution (the default system).</summary>
    public static GameDataRepository Repo => LazyRepo.Value;

    private static readonly Dictionary<string, GameDataRepository> RepoCache = new();

    /// <summary>
    /// A fully-loaded repository for a specific game system, cached across tests. Use this in
    /// preference to hand-rolling the service stack — the wiring order matters, and a
    /// <see cref="GameSystemService"/> left uninitialised silently serves the default system's data.
    /// </summary>
    public static GameDataRepository RepoFor(string systemKey)
    {
        lock (RepoCache)
        {
            if (RepoCache.TryGetValue(systemKey, out var cached))
                return cached;

            var repo = CreateRepository(systemKey);
            RepoCache[systemKey] = repo;
            return repo;
        }
    }

    /// <summary>
    /// The active system's Troop Types figures, for tests that want the numbers without
    /// resolving a unit.
    /// </summary>
    public static Func<BaseTroopType, TroopTypeRules> TroopTypeRulesFor(string systemKey)
        => RepoFor(systemKey).GetTroopTypeRules;

    /// <summary>A unit of the shared repository by faction id and book name, case-insensitive.</summary>
    public static async Task<Unit> FindUnitAsync(string factionId, string name, string systemKey = GameSystems.Default)
    {
        var units = await RepoFor(systemKey).GetUnitsAsync(factionId);
        return units.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"{factionId} has no unit named '{name}'. Units: {string.Join(", ", units.Select(u => u.Name))}");
    }

    public static string FindProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Warhammer.csproj")))
                return dir;
            var sibling = Path.Combine(Path.GetDirectoryName(dir)!, "Warhammer");
            if (Directory.Exists(sibling) && File.Exists(Path.Combine(sibling, "Warhammer.csproj")))
                return sibling;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find Warhammer project root");
    }

    public static CoreRulesService CreateCoreRules()
    {
        var service = new CoreRulesService(null!, new GameSystemService());
        service.Initialize(CoreData);
        return service;
    }

    /// <summary>
    /// Create a fully-loaded GameDataRepository backed by the on-disk wwwroot data.
    /// Weapons, armour, troop type figures and the bundle's faction list are all resolved.
    /// </summary>
    public static GameDataRepository CreateRepository(string systemKey = GameSystems.Default)
    {
        var wwwroot = Path.Combine(FindProjectRoot(), "wwwroot");
        var handler = new FileBasedHttpHandler(wwwroot);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var system = new GameSystemService();
        system.Initialize(systemKey);
        var coreRules = new CoreRulesService(http, system);
        var bundle = new BundleService(http, system);
        var repo = new GameDataRepository(coreRules, bundle, system);
        repo.EnsureLoadedAsync().GetAwaiter().GetResult();
        return repo;
    }

    /// <summary>
    /// Build a fully-resolved <see cref="Unit"/> with real weapon/armour data from core-rules.json.
    /// Equipment strings are parsed and resolved to domain objects.
    /// </summary>
    public static Unit MakeUnit(
        string name = "Test Unit",
        string troopType = "Infantry",
        int m = 4, int ws = 4, int bs = 3, int s = 3, int t = 3, int w = 1, int i = 3, int a = 1, int ld = 7,
        double? points = 8,
        string? equipment = "Hand weapon",
        string? specialRules = null,
        string? unitSize = "10+",
        string? baseSize = "25x25",
        ProfileEntry? mountProfile = null,
        List<OptionGroup>? options = null,
        string systemKey = GameSystems.Default,
        UnitCategory category = UnitCategory.Core,
        IReadOnlyList<ProfileEntry>? profiles = null,
        IReadOnlyDictionary<string, MountOption>? mountOptions = null)
    {
        // Everything — weapons, armour and the troop type figures — comes from the one system,
        // so a unit can't end up with one edition's troop type wearing another's weapon rules.
        var repo = RepoFor(systemKey);

        // When the caller supplies the entry's priced profiles, the first is the one fielded by
        // default, as the repository does; otherwise the stats given build the one profile.
        var primaryProfile = profiles is { Count: > 0 }
            ? profiles[0]
            : new ProfileEntry
            {
                Id = Slug(name), Name = name,
                M = m, WS = ws, BS = bs, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points,
            };

        var parsedTroopType = TroopTypeInfo.Parse(troopType, repo.GetTroopTypeRules);
        var parsedRules = ParsedSpecialRules.Parse(specialRules);

        // Resolve equipment
        var (weaponStringKeys, armourStringKeys) = EquipmentParser.Parse(equipment);
        var weapons = new List<Weapon>();
        foreach (var wKey in weaponStringKeys)
        {
            if (CloseCombatWeaponKeyExtensions.TryFromJsonKey(wKey, out var wk))
            {
                var weapon = repo.GetWeapon(wk);
                if (weapon != null) weapons.Add(weapon);
            }
        }

        var armour = new List<Armour>();
        foreach (var aKey in armourStringKeys)
        {
            if (ArmourKeyExtensions.TryFromJsonKey(aKey, out var ak))
            {
                var arm = repo.GetArmour(ak);
                if (arm != null) armour.Add(arm);
            }
        }

        var baseModel = new Model
        {
            Name = name,
            Stats = primaryProfile,
            TroopType = parsedTroopType,
            SpecialRules = parsedRules,
            Weapons = weapons,
            Armour = armour,
        };

        Model? mount = null;
        if (mountProfile != null && parsedTroopType.HasSplitProfile)
        {
            mount = new Model
            {
                Name = mountProfile.Name,
                Stats = mountProfile,
                TroopType = parsedTroopType,
                SpecialRules = parsedRules,
                Weapons = [],
                Armour = [],
            };
        }

        var groups = options ?? [];

        // Build the raw UnitEntry for RawEntry
        var allProfiles = new List<ProfileEntry>(profiles ?? [primaryProfile]);
        if (mountProfile != null) allProfiles.Add(mountProfile);

        var rawEntry = new UnitEntry
        {
            Id = Slug(name),
            Name = name,
            Section = category.ToSectionKey(),
            TroopType = troopType,
            Profiles = allProfiles,
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize,
            BaseSize = baseSize,
            OptionGroups = groups,
        };

        // Build available armour dictionary from repo (all armour types)
        var availableArmour = new Dictionary<ArmourKey, Armour>();
        foreach (var ak in Enum.GetValues<ArmourKey>())
        {
            var arm = repo.GetArmour(ak);
            if (arm != null) availableArmour[ak] = arm;
        }

        return new Unit
        {
            Id = Slug(name),
            Name = name,
            FactionId = FixtureFaction,
            Category = category,
            BaseModel = baseModel,
            Profiles = profiles ?? [primaryProfile],
            Mount = mount,
            MountOptions = mountOptions ?? new Dictionary<string, MountOption>(),
            BaseWidthMm = UnitConfiguration.ParseBaseWidthMm(baseSize),
            OptionGroups = groups,
            MinUnitSize = parsedTroopType.IsCharacter ? 1 : UnitConfiguration.ParseMinUnitSize(unitSize),
            MaxUnitSize = null,
            BaseModelCost = primaryProfile.Points ?? points ?? 0,
            Command = CommandAvailability.FromGroups(groups),
            AvailableArmour = availableArmour,
            RawEntry = rawEntry,
        };
    }

    /// <summary>kebab-case, as the bundle derives ids from names.</summary>
    public static string Slug(string text) => FactionUpgrades.Slug(text);

    // ─── Option group fixtures ──────────────────────────────────────

    /// <summary>An equipment group: "May choose one of the following" (select one) or independent adds.</summary>
    public static OptionGroup Equipment(string id, SelectMode select, params OptionChoice[] choices) => new()
    {
        Id = id,
        Kind = OptionKind.Equipment.ToJsonKey(),
        Select = select.ToString().ToLowerInvariant(),
        Min = 0,
        Max = select == SelectMode.One ? 1 : choices.Length,
        Raw = select == SelectMode.One ? "May choose one of the following:" : "May take any of the following:",
        Choices = [.. choices],
    };

    /// <summary>A single-line equipment option — "May take a shield +1 point/model".</summary>
    public static OptionGroup Equipment(string name, double? points, bool perModel = false, string? replaces = null) => new()
    {
        Id = Slug(name),
        Kind = OptionKind.Equipment.ToJsonKey(),
        Select = "any",
        Min = 0,
        Max = 1,
        Raw = $"May take {name}",
        Choices = [Choice(name, points, perModel, replaces)],
    };

    public static OptionChoice Choice(string name, double? points, bool perModel = false, string? replaces = null) => new()
    {
        Id = Slug(name),
        Name = name,
        Points = points,
        PerModel = perModel,
        Replaces = replaces,
        Raw = points is null ? name : $"{name} +{points} points{(perModel ? "/model" : "")}",
    };

    /// <summary>A command group with a nested magic standard allowance when a banner cap is given.</summary>
    public static OptionGroup Command(CommandRole role, double points, int? bannerCap = null, bool bannerUnlimited = false)
    {
        var roleKey = role switch
        {
            CommandRole.Champion => "champion",
            CommandRole.Musician => "musician",
            _ => "standardBearer",
        };
        var choice = new OptionChoice
        {
            Id = roleKey, Name = role.ToString(), Points = points, Raw = $"May upgrade one model to a {role} +{points} points",
        };
        if (bannerCap is not null || bannerUnlimited)
        {
            choice = choice with
            {
                Options =
                [
                    new OptionGroup
                    {
                        Id = "magic-standard", Kind = OptionKind.MagicStandard.ToJsonKey(), Select = "any",
                        Min = 0, Max = 1, PointsLimit = bannerCap,
                        Raw = bannerCap is null ? "May take a Magic Standard with no points limit" : $"May take a Magic Standard worth up to {bannerCap} points",
                        Choices = [new OptionChoice { Id = "magic-standard", Name = "Magic Standard", Raw = "May take a Magic Standard" }],
                    },
                ],
            };
        }

        return new OptionGroup
        {
            Id = roleKey, Kind = OptionKind.Command.ToJsonKey(), Select = "any", Min = 0, Max = 1,
            Role = roleKey, Raw = choice.Raw, Choices = [choice],
        };
    }

    /// <summary>A magic item allowance, optionally for one profile, one list, or a family too.</summary>
    public static OptionGroup MagicItems(int? limit, string? appliesTo = null, List<string>? itemTypes = null, List<string>? families = null) => new()
    {
        Id = appliesTo is null ? "magic-items" : $"magic-items-{appliesTo}",
        Kind = OptionKind.MagicItems.ToJsonKey(),
        Select = "any", Min = 0, Max = 1,
        AppliesTo = appliesTo,
        PointsLimit = limit,
        ItemTypes = itemTypes,
        Families = families,
        Raw = appliesTo is null
            ? $"May take Magic Items up to a total of {limit} points"
            : $"A {appliesTo} may take Magic Items up to a total of {limit} points",
        Choices = [new OptionChoice { Id = "magic-items", Name = "Magic Items", Raw = "Magic Items" }],
    };

    public static OptionGroup BattleStandard(double points, string? appliesTo = null) => new()
    {
        Id = "battle-standard", Kind = OptionKind.BattleStandard.ToJsonKey(), Select = "any", Min = 0, Max = 1,
        AppliesTo = appliesTo, Points = points,
        Raw = $"May carry the Battle Standard +{points} points",
        Choices = [new OptionChoice { Id = "battle-standard", Name = "Battle Standard", Points = points, Raw = "Battle Standard" }],
    };

    public static OptionGroup WizardLevel(double points) => new()
    {
        Id = "wizard-level", Kind = OptionKind.WizardLevel.ToJsonKey(), Select = "any", Min = 0, Max = 1,
        Raw = $"May take an additional Wizard Level +{points} points",
        Choices = [new OptionChoice { Id = "additional-wizard-level", Name = "additional Wizard Level", Points = points, LevelDelta = 1, Raw = "additional Wizard Level" }],
    };

    /// <summary>A faction upgrade family offered by a group, with an optional per-pick rate.</summary>
    public static OptionGroup FactionUpgrade(string family, double? rate = null, bool perModel = false, int? pointsLimit = null) => new()
    {
        Id = family, Kind = OptionKind.FactionUpgrade.ToJsonKey(), Select = "any", Min = 0, Max = 1,
        Family = family, PointsLimit = pointsLimit,
        Raw = $"May take {family.Replace('-', ' ')}",
        Choices = [new OptionChoice { Id = family, Name = family.Replace('-', ' '), Points = rate, PerModel = perModel, Raw = $"May take {family}" }],
    };

    /// <summary>A mount menu whose choices name the mounts by id.</summary>
    public static OptionGroup Mounts(params (string Name, double Points, string? UnitId)[] mounts) => new()
    {
        Id = "mount", Kind = OptionKind.Mount.ToJsonKey(), Select = "one", Min = 0, Max = 1,
        Raw = "May be mounted on one of the following:",
        Choices = mounts.Select(m => new OptionChoice
        {
            Id = Slug(m.Name), Name = m.Name, Points = m.Points, UnitId = m.UnitId, Raw = $"{m.Name} +{m.Points} points",
        }).ToList(),
    };

    // ─── Statistical Helpers ────────────────────────────────────────

    /// <summary>
    /// Runs a combat scenario many times and aggregates AttackResult totals.
    /// </summary>
    public static AggregatedAttackResult RunAttackTrials(
        int iterations,
        Func<Random, AttackResult> trial)
    {
        long totalHits = 0, totalWounds = 0, totalKBWounds = 0, totalUnsaved = 0;

        for (int seed = 0; seed < iterations; seed++)
        {
            var rng = new Random(seed);
            var result = trial(rng);
            totalHits += result.TotalHits;
            totalWounds += result.TotalWoundsInflicted;
            totalKBWounds += result.KillingBlowWounds;
            totalUnsaved += result.UnsavedWounds;
        }

        return new AggregatedAttackResult(iterations, totalHits, totalWounds, totalKBWounds, totalUnsaved);
    }

    /// <summary>
    /// Runs a combat round scenario many times and aggregates RoundResult totals.
    /// </summary>
    public static AggregatedRoundResult RunRoundTrials(
        int iterations,
        Func<Random, RoundResult> trial)
    {
        long totalAttWounds = 0, totalDefWounds = 0;

        for (int seed = 0; seed < iterations; seed++)
        {
            var rng = new Random(seed);
            var result = trial(rng);
            totalAttWounds += result.AttackerWoundsDealt;
            totalDefWounds += result.DefenderWoundsDealt;
        }

        return new AggregatedRoundResult(iterations, totalAttWounds, totalDefWounds);
    }

    /// <summary>
    /// Asserts that an observed rate falls within an expected range.
    /// </summary>
    public static void AssertRate(double observed, double expectedMin, double expectedMax, string label)
    {
        Assert.True(observed >= expectedMin && observed <= expectedMax,
            $"{label}: expected [{expectedMin:F3}–{expectedMax:F3}], got {observed:F3}");
    }

    /// <summary>
    /// Asserts that A is meaningfully greater than B (by at least minRatio).
    /// </summary>
    public static void AssertGreaterByRatio(long a, long b, double minRatio, string label)
    {
        double ratio = b > 0 ? (double)a / b : double.MaxValue;
        Assert.True(ratio >= minRatio,
            $"{label}: expected ratio >= {minRatio:F2}, got {ratio:F3} (A={a}, B={b})");
    }
}

public record AggregatedAttackResult(int Trials, long TotalHits, long TotalWounds, long TotalKBWounds, long TotalUnsaved)
{
    public double AvgHits => (double)TotalHits / Trials;
    public double AvgWounds => (double)TotalWounds / Trials;
    public double AvgKBWounds => (double)TotalKBWounds / Trials;
    public double AvgUnsaved => (double)TotalUnsaved / Trials;
    public double HitRate(int attacksPerTrial) => (double)TotalHits / (Trials * attacksPerTrial);
    public double WoundRate(int attacksPerTrial) => (double)TotalWounds / (Trials * attacksPerTrial);
}

public record AggregatedRoundResult(int Trials, long TotalAttackerWounds, long TotalDefenderWounds)
{
    public double AvgAttackerWounds => (double)TotalAttackerWounds / Trials;
    public double AvgDefenderWounds => (double)TotalDefenderWounds / Trials;
}

/// <summary>
/// HttpMessageHandler that reads files from a local directory, mapping URL paths to file paths.
/// e.g. GET http://localhost/data/war/core-rules.json → {baseDir}/data/war/core-rules.json
/// </summary>
public class FileBasedHttpHandler : HttpMessageHandler
{
    private readonly string _baseDir;

    public FileBasedHttpHandler(string baseDir) => _baseDir = baseDir;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var filePath = Path.Combine(_baseDir, path.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(filePath))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"File not found: {filePath}")
            });
        }

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
