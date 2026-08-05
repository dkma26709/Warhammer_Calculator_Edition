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

    static TestHelpers()
    {
        var dataDir = Path.Combine(FindProjectRoot(), "wwwroot", "data", "wap-v2");
        var json = File.ReadAllText(Path.Combine(dataDir, "core-rules.json"));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        CoreData = JsonSerializer.Deserialize<CoreRulesData>(json, options)!;
        LazyRepo = new Lazy<GameDataRepository>(() => CreateRepository());
    }

    /// <summary>Shared repository instance for test unit resolution (wap-v2).</summary>
    public static GameDataRepository Repo => LazyRepo.Value;

    private static readonly Dictionary<string, GameDataRepository> RepoCache = new();

    /// <summary>
    /// A fully-loaded repository for a specific game system, cached across tests. Use this in
    /// preference to hand-rolling the service stack — the wiring order matters, and a
    /// <see cref="GameSystemService"/> left uninitialised silently serves wap-v2 data.
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
    /// Weapons, armour, troop type figures and the system manifest are all resolved.
    /// </summary>
    public static GameDataRepository CreateRepository(string systemKey = GameSystems.Default)
    {
        var wwwroot = Path.Combine(FindProjectRoot(), "wwwroot");
        var handler = new FileBasedHttpHandler(wwwroot);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var system = new GameSystemService();
        system.Initialize(systemKey);
        var coreRules = new CoreRulesService(http, system);
        var factionData = new FactionDataService(http, system);
        var repo = new GameDataRepository(coreRules, factionData, system);
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
        List<UnitOption>? options = null,
        string systemKey = GameSystems.Default)
    {
        // Everything — weapons, armour and the troop type figures — comes from the one system,
        // so a unit can't end up with one edition's troop type wearing another's weapon rules.
        var repo = RepoFor(systemKey);
        var primaryProfile = new ProfileEntry
        {
            Name = name, M = m, WS = ws, BS = bs, S = s, T = t, W = w, I = i, A = a, Ld = ld, Points = points
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

        // Resolve command availability
        var command = ResolveCommand(options);

        // Build the raw UnitEntry for RawEntry
        var profiles = new List<ProfileEntry> { primaryProfile };
        if (mountProfile != null) profiles.Add(mountProfile);

        var rawEntry = new UnitEntry
        {
            Name = name,
            TroopType = troopType,
            Profiles = profiles,
            Equipment = equipment,
            SpecialRules = specialRules,
            UnitSize = unitSize,
            BaseSize = baseSize,
            Options = options,
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
            Name = name,
            Faction = FactionKey.TheEmpire,
            Category = UnitCategory.Core,
            BaseModel = baseModel,
            Mount = mount,
            BaseWidthMm = UnitConfiguration.ParseBaseWidthMm(baseSize),
            Options = options ?? [],
            MinUnitSize = parsedTroopType.IsCharacter ? 1 : UnitConfiguration.ParseMinUnitSize(unitSize),
            MaxUnitSize = null,
            BaseModelCost = points ?? 0,
            Command = command,
            AvailableArmour = availableArmour,
            RawEntry = rawEntry,
        };
    }

    private static CommandAvailability ResolveCommand(List<UnitOption>? options)
    {
        if (options == null || options.Count == 0)
            return CommandAvailability.None;

        bool canChamp = false, canMus = false, canStd = false;
        double champCost = 0, musCost = 0, stdCost = 0;

        foreach (var opt in options)
        {
            if (opt.Type != "command" || opt.Role == null) continue;
            double cost = opt.Points ?? 0;

            if (!CommandRoleExtensions.TryFromRoleText(opt.Role, out var role))
                continue;

            switch (role)
            {
                case CommandRole.Champion:
                    canChamp = true;
                    champCost = cost;
                    break;
                case CommandRole.Musician:
                    canMus = true;
                    musCost = cost;
                    break;
                case CommandRole.StandardBearer:
                    canStd = true;
                    stdCost = cost;
                    break;
            }
        }

        return new CommandAvailability
        {
            CanHaveChampion = canChamp,
            ChampionCost = champCost,
            CanHaveMusician = canMus,
            MusicianCost = musCost,
            CanHaveStandardBearer = canStd,
            StandardBearerCost = stdCost,
        };
    }

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
/// e.g. GET http://localhost/data/wap-v2/core-rules.json → {baseDir}/data/wap-v2/core-rules.json
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
